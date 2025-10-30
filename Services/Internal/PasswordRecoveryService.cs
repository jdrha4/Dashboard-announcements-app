using Application.Areas.Account.Models;
using Application.Configuration;
using Application.Core;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Services.Internal;

/// <summary>
/// Service responsible for handling password recovery functionality,
/// including reset token generation, validation, and email dispatch.
/// </summary>
public class PasswordRecoveryService
{
    private readonly DatabaseContext _db;
    private readonly ILogger<PasswordRecoveryService> _logger;
    private readonly PasswordResetSettings _settings;

    public PasswordRecoveryService(
        DatabaseContext db,
        ILogger<PasswordRecoveryService> logger,
        IOptions<PasswordResetSettings> settings
    )
    {
        _db = db;
        _logger = logger;
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Removes all active password reset tokens associated with the given user.
    /// </summary>
    /// <param name="user">The user for whom tokens should be reset.</param>
    public async Task ResetUserTokensAsync(UserDo user)
    {
        _logger.LogDebug("Resetting old tokens for {Email} ({UserId})", user.Email, user.Id);
        _db.PasswordResetTokens.RemoveRange(_db.PasswordResetTokens.Where(t => t.UserId == user.Id));
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Verifies a given password reset token and returns the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="token">The password reset token to verify.</param>
    /// <returns>The user associated with the token if valid; otherwise, null.</returns>
    public async Task<UserDo?> CheckResetTokenAsync(string token)
    {
        PasswordResetToken? resetToken = await _db
            .PasswordResetTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.Expiration > DateTime.UtcNow);

        return resetToken?.User;
    }

    /// <summary>
    /// Generates a new password reset token for the given user if the user does not already have too many active tokens.
    /// </summary>
    /// <param name="user">The user for whom the token should be generated.</param>
    /// <returns>The generated token string if successful; otherwise, null.</returns>
    public async Task<string?> GenerateTokenAsync(UserDo user)
    {
        List<PasswordResetToken> activeTokens = await _db
            .PasswordResetTokens.Where(t => t.UserId == user.Id && t.Expiration > DateTime.UtcNow)
            .ToListAsync();

        if (activeTokens.Count >= _settings.MaxActiveTokens)
        {
            _logger.LogWarning(
                "Skipping password reset token generation for {Email} ({UserId}): maximum token limit",
                user.Email,
                user.Id
            );
            return null;
        }

        string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        PasswordResetToken? resetToken = new PasswordResetToken
        {
            Token = token,
            Expiration = DateTime.UtcNow.AddSeconds(_settings.TokenExpirySeconds),
            UserId = user.Id,
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Password Reset token generated for {Email} ({UserId}): {Token}", user.Email, user.Id, token);

        return token;
    }
}
