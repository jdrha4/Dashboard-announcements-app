using System.Security.Claims;
using Application.Configuration;
using Application.Core;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Migrations;
using Application.Infrastructure.Database.Models;
using Application.Services.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Handles business logic for account management, registration, and updates.
/// </summary>
public class AccountService
{
    private readonly DatabaseContext _db;
    private readonly EmailService _emailSender;
    private readonly EmailConfirmationService _emailConfirmation;
    private readonly PasswordRecoveryService _passwordRecovery;
    private readonly ILogger<AccountService> _logger;

    public bool IsConfirmationEnabled => _emailConfirmation.IsConfirmationEnabled;

    public AccountService(
        DatabaseContext db,
        EmailService emailSender,
        EmailConfirmationService emailConfirmation,
        PasswordRecoveryService passwordRecovery,
        ILogger<AccountService> logger
    )
    {
        _db = db;
        _emailSender = emailSender;
        _emailConfirmation = emailConfirmation;
        _passwordRecovery = passwordRecovery;
        _logger = logger;
    }

    #region Register


    /// <summary>
    /// Checks if the given email address can be registered based on global settings.
    /// </summary>
    /// <remarks>
    /// This check alone isn't sufficient to allow registration, as it does not check
    /// whether the email is already registered. See <see cref="EmailExistsAsync"/> for that.
    /// </remarks>
    /// <param name="email">The email to validate.</param>
    /// <returns>True if registration is allowed for this email; otherwise, false.</returns>
    public async Task<bool> EmailIsAllowedAsync(string email)
    {
        var globalSettings = await _db.GlobalSettings.Include(gs => gs.AllowedEmailDomains).FirstOrDefaultAsync();

        if (globalSettings == null || globalSettings.AllowedEmailDomains.Count == 0)
            return true;

        var userDomain = email.Split('@')[1].ToLowerInvariant();
        return globalSettings.AllowedEmailDomains.Any(d =>
            d.Domain.Equals(userDomain, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Registers a new user with the specified credentials.
    /// </summary>
    /// <remarks>
    /// This method does not validate whether the email is allowed or already registered.
    /// You must perform these checks separately. See <see cref="EmailIsAllowedAsync"/> and
    /// <see cref="EmailExistsAsync"/>.
    /// </remarks>
    /// <param name="email">User email.</param>
    /// <param name="name">User display name.</param>
    /// <param name="password">User password.</param>
    /// <returns>The created <see cref="UserDo"/> object.</returns>
    public async Task<UserDo> RegisterUserAsync(string email, string name, string password)
    {
        var (salt, hash) = Password.Create(password);

        var newUser = new UserDo
        {
            Email = email,
            Name = name,
            PasswordSalt = salt,
            PasswordHash = hash,
            Role = UserRole.User,
            Confirmed = !_emailConfirmation.IsConfirmationEnabled,
        };

        await _db.Users.AddAsync(newUser);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {Email} ({UserId}) was registered", newUser.Email, newUser.Id);

        return newUser;
    }

    /// <summary>
    /// Generates a confirmation token and sends the confirmation email asynchronously.
    /// </summary>
    /// <remarks>
    /// The email is sent in a background task, to avoid blocking until it's sent, as email
    /// sending can be a fairly slow operation.
    /// </remarks>
    /// <param name="user">The user to confirm.</param>
    /// <param name="generateConfirmationUrl">A function that receives the token and returns the full confirmation URL.</param>
    public async Task SendConfirmationEmailAsync(UserDo user, Func<string, string> generateConfirmationUrl)
    {
        string token = await _emailConfirmation.GenerateTokenAsync(user);
        string confirmUrl = generateConfirmationUrl(token);

        _ = Task.Run(async () =>
        {
            // Exceptions that occur in Task.Run aren't logged normally,
            // log manually just in case one does occur.
            try
            {
                await _emailSender.SendUserConfirmEmail(user.Email, confirmUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending confirmation email.");
            }
        });
    }

    /// <summary>
    /// Confirms the user's email using the given token. If valid, marks the user as confirmed.
    /// </summary>
    /// <param name="token">The email confirmation token to verify.</param>
    /// <returns>The confirmed user if successful; otherwise, null.</returns>
    public async Task<UserDo?> ConfirmEmailAsync(string token)
    {
        return await _emailConfirmation.ConfirmEmailAsync(token);
    }

    #endregion

    #region Login

    /// <summary>
    /// Attempts to find a user by email and verify the given password.
    ///
    /// Does not indicate whether the failure was missing user or bad password,
    /// to prevent information leakage.
    /// </summary>
    /// <remarks>
    /// Does not validate whether the user account is or isn't confirmed, this check
    /// should be performed afterwards. See: <see cref=""/>
    /// </remarks>
    /// <param name="email">The login email address.</param>
    /// <param name="password">The plaintext password to verify.</param>
    /// <returns>
    /// The matching UserDo if credentials are valid; otherwise null.
    /// </returns>
    public async Task<UserDo?> AuthenticateUserAsync(string email, string password)
    {
        UserDo? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogDebug("Login attempt failed, no such email ({Email})", email);
            return null;
        }

        if (!ValidatePasswordAsync(user, password))
        {
            _logger.LogDebug("Login attempt for {Email} failed, wrong password", email);
            return null;
        }

        return user;
    }

    /// <summary>
    /// Determines whether the user with the given ID has a confirmed email address.
    /// </summary>
    /// <remarks>
    /// If email confirmation is disabled, this always returns true.
    /// </remarks>
    /// <param name="userId">The ID of the user to check.</param>
    /// <returns>
    /// True if the user's email is confirmed, false if not.
    /// </returns>
    public async Task<bool> IsUserConfirmedAsync(Guid userId)
    {
        if (!_emailConfirmation.IsConfirmationEnabled)
        {
            return true; // Confirmation not required
        }

        return await _emailConfirmation.IsEmailConfirmedAsync(userId);
    }

    /// <summary>
    /// Validates a given password against the user's stored credentials.
    /// </summary>
    /// <param name="user">The user to validate against.</param>
    /// <param name="password">The plaintext password to validate.</param>
    /// <returns>True if the password is valid; otherwise, false.</returns>
    public bool ValidatePasswordAsync(UserDo user, string password)
    {
        return Password.Verify(password, user.PasswordHash, user.PasswordSalt);
    }

    # endregion

    # region Cookies

    /// <summary>
    /// Signs in the given user by issuing an authentication cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="user">The authenticated user.</param>
    public async Task SignInUserAsync(HttpContext httpContext, UserDo user)
    {
        var claims = new List<Claim>
        {
            new("Id", user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties { AllowRefresh = true, IsPersistent = true };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    /// <summary>
    /// Sign out the currently logged in user by revoking the authentication cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task signOutUserAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    #endregion

    #region Password Reset

    /// <summary>
    /// Resets the user's password and invalidates all existing reset tokens.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="newPassword">The new plaintext password.</param>
    public async Task ResetUserPasswordAsync(UserDo user, string newPassword)
    {
        await ChangeUserPasswordAsync(user, newPassword, false);

        await _passwordRecovery.ResetUserTokensAsync(user);

        _logger.LogInformation("Password was reset for user: {Email} ({UserId})", user.Email, user.Id);
    }

    /// <summary>
    /// Generates a password reset token and sends the reset email asynchronously.
    /// </summary>
    /// <remarks>
    /// The email is sent in a background task, to avoid blocking until it's sent, as email
    /// sending can be a fairly slow operation.
    ///
    /// This also reduces the risk of timing attacks that might reveal account existence.
    /// </remarks>
    /// <param name="user">The user requesting the reset.</param>
    /// <param name="generateResetUrl">A function that receives the token and returns the full reset URL.</param>
    public async Task SendPasswordResetEmailAsync(UserDo user, Func<string, string> generateResetUrl)
    {
        string? token = await _passwordRecovery.GenerateTokenAsync(user);
        if (token == null)
            return;

        string resetUrl = generateResetUrl(token);

        _ = Task.Run(async () =>
        {
            // Exceptions that occur in Task.Run aren't logged normally,
            // log manually just in case one does occur.
            try
            {
                await _emailSender.SendPasswordResetEmail(user.Email, resetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending password reset email.");
            }
        });
    }

    /// <summary>
    /// Verifies a given password reset token and returns the associated user if the token is valid and not expired.
    /// </summary>
    /// <param name="token">The password reset token to verify.</param>
    /// <returns>The user associated with the token if valid; otherwise, null.</returns>
    public async Task<UserDo?> CheckResetTokenAsync(string token)
    {
        return await _passwordRecovery.CheckResetTokenAsync(token);
    }

    # endregion

    # region Utilities

    /// <summary>
    /// Checks whether an account with the given email already exists.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <returns>True if the email is already registered; otherwise, false.</returns>
    /// <seealso cref="EmailIsAllowedAsync"/>
    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _db.Users.AnyAsync(u => u.Email == email);
    }

    /// <summary>
    /// Changes the user's password to a new one.
    /// </summary>
    /// <param name="user">The user whose password is being changed.</param>
    /// <param name="newPassword">The new plaintext password.</param>
    public async Task ChangeUserPasswordAsync(UserDo user, string newPassword)
    {
        await ChangeUserPasswordAsync(user, newPassword, true);
    }

    /// <summary>
    /// Changes the user's password to a new one.
    /// </summary>
    /// <param name="user">The user whose password is being changed.</param>
    /// <param name="newPassword">The new plaintext password.</param>
    /// <param name="shouldLog">Should a log message be emitted?</param>
    private async Task ChangeUserPasswordAsync(UserDo user, string newPassword, bool shouldLog)
    {
        var (salt, hash) = Password.Create(newPassword);
        user.PasswordSalt = salt;
        user.PasswordHash = hash;
        await _db.SaveChangesAsync();

        if (shouldLog)
            _logger.LogInformation("Password changed for user: {Email} ({UserId})", user.Email, user.Id);
    }

    /// <summary>
    /// Update various fields of the user.
    /// </summary>
    /// <remarks>
    /// All fields are required, this will perform a PUT-like update, not a partial PATCH-like update.
    /// </remarks>
    public async Task UpdateUserAsync(UserDo user, string name, string email, string? profileImageB64)
    {
        user.Name = name;
        user.Email = email;
        user.ProfileImageBase64 = profileImageB64;
        await _db.SaveChangesAsync();

        _logger.LogInformation("User account {Email} ({UserId}) was edited", user.Email, user.Id);
    }

    #endregion
}
