using Application.Configuration;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Migrations;
using Application.Infrastructure.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Services.Internal;

/// <summary>
/// Service responsible for handling user email confirmations after registrations.
/// This includes generating and storing confirmation tokens,
/// sending confirmation emails, validating tokens, alongside logic to check if user
/// is confirmed.
/// </summary>
public class EmailConfirmationService
{
    private readonly DatabaseContext _db;
    private readonly ILogger<EmailConfirmationService> _logger;
    private readonly AccountConfirmationSettings _settings;

    public bool IsConfirmationEnabled => _settings.RequireConfirmation;

    public EmailConfirmationService(
        DatabaseContext db,
        ILogger<EmailConfirmationService> logger,
        IOptions<AccountConfirmationSettings> settings
    )
    {
        _db = db;
        _logger = logger;
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Confirms the user's email using the given token. If valid, marks the user as confirmed.
    /// </summary>
    /// <param name="token">The email confirmation token to verify.</param>
    /// <returns>The confirmed user if successful; otherwise, null.</returns>
    public async Task<UserDo?> ConfirmEmailAsync(string token)
    {
        EmailConfirmationToken? record = await _db
            .EmailConfirmationTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.Expiration > DateTime.UtcNow);

        if (record == null)
        {
            _logger.LogDebug("Email confirmation token check failed: {Token}", token);
            return null;
        }

        UserDo user = record.User;
        user.Confirmed = true;

        _db.EmailConfirmationTokens.Remove(record);
        await _db.SaveChangesAsync();

        _logger.LogDebug("Email confirmation succeeded for {Email} ({UserId})", user.Email, user.Id);

        return user;
    }

    /// <summary>
    /// Checks whether the user with the given ID has confirmed their email.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the user has confirmed their email; otherwise, false.</returns>
    public async Task<bool> IsEmailConfirmedAsync(Guid userId)
    {
        return await _db.Users.AnyAsync(u => u.Id == userId && u.Confirmed);
    }

    /// <summary>
    /// Generates a new confirmation token for the given user and stores it in the database.
    /// </summary>
    /// <remarks>
    /// This will also remove any previous (expired) tokens associated with the user.
    /// </remarks>
    /// <param name="user">The user for whom the token is being generated.</param>
    /// <returns>The generated token string</returns>
    public async Task<string> GenerateTokenAsync(UserDo user)
    {
        _logger.LogDebug("Generating email confirmation token for {Email} ({UserId})", user.Email, user.Id);

        // Clean up any existing tokens for this user
        _db.EmailConfirmationTokens.RemoveRange(_db.EmailConfirmationTokens.Where(t => t.UserId == user.Id));

        string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        DateTime expires = DateTime.UtcNow.AddSeconds(_settings.TokenExpirySeconds);

        _db.EmailConfirmationTokens.Add(
            new EmailConfirmationToken
            {
                Token = token,
                UserId = user.Id,
                Expiration = expires,
            }
        );

        await _db.SaveChangesAsync();

        _logger.LogDebug("Token generated for {Email} ({UserId}): {Token}", user.Email, user.Id, token);

        return token;
    }

    public async Task CleanupExpiredTokensAndUsersAsync()
    {
        // Find all expired tokens whose users are still unconfirmed
        var expiredTokens = await _db
            .EmailConfirmationTokens.Include(t => t.User)
            .Where(t => t.Expiration <= DateTime.UtcNow && !t.User.Confirmed)
            .ToListAsync();

        if (expiredTokens.Count == 0)
            return;

        var usersToRemove = expiredTokens.Select(t => t.User).Distinct().ToList();

        // Remove tokens and users
        _db.EmailConfirmationTokens.RemoveRange(expiredTokens);
        _db.Users.RemoveRange(usersToRemove);

        // Persist in one transaction
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Cleaned up {TokenCount} expired tokens and {UserCount} unconfirmed users",
            expiredTokens.Count,
            usersToRemove.Count
        );
    }
}
