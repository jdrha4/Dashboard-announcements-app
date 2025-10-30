using System;
using System.Threading.Tasks;
using Application.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpAuthException = System.Security.Authentication.AuthenticationException;

namespace Application.Services;

/// <summary>
/// Service responsible for sending emails using SMTP or logging mode based on configuration.
/// </summary>
public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly IWebHostEnvironment _env;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger, IWebHostEnvironment env)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>
    /// Sends a password reset email to the specified user.
    /// </summary>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="resetUrl">The URL to reset the user's password.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendPasswordResetEmail(string email, string resetUrl)
    {
        await SendEmailAsync(
            email,
            "Password Reset",
            $"<p>Click <a href='{resetUrl}'>here</a> to reset your password.</p>"
        );
    }

    /// <summary>
    /// Sends an email to the user asking them to confirm their registration.
    /// </summary>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="confirmUrl">The confirmation URL to be included in the email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendUserConfirmEmail(string email, string confirmUrl)
    {
        await SendEmailAsync(
            email,
            "Confirm your registration",
            $"<p>Welcome! Please <a href='{confirmUrl}'>confirm your registration</a>.</p>"
        );
    }

    /// <summary>
    /// Sends an email to the specified recipient using the configured email mode.
    /// </summary>
    /// <param name="toEmail">The recipient's email address.</param>
    /// <param name="subject">The subject of the email.</param>
    /// <param name="message">The HTML body of the email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="SmtpCommandException">Thrown for SMTP command errors.</exception>
    /// <exception cref="SmtpProtocolException">Thrown for SMTP protocol violations.</exception>
    /// <exception cref="AuthenticationException">Thrown for authentication failures.</exception>
    /// <exception cref="Exception">Thrown for unexpected exceptions during email sending.</exception>
    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        if (_env.IsProduction() && _settings.Mode != EmailMode.Smtp)
        {
            _logger.LogWarning(
                "Email mode is set to '{Mode}' in production. Only 'smtp' should be used in production environments.",
                _settings.Mode
            );
        }

        if (_settings.Mode == EmailMode.Log)
        {
            _logger.LogInformation(
                "Email to {Recipient} would have contained: [{Subject}] {Message}",
                toEmail,
                subject,
                message
            );
            return;
        }

        if (_settings.Mode != EmailMode.Smtp)
        {
            _logger.LogError(
                "Unknown email mode '{Mode}'. Email to {Recipient} was not sent.",
                _settings.Mode,
                toEmail
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.SmtpServer) || string.IsNullOrWhiteSpace(_settings.Username))
        {
            _logger.LogError(
                "SMTP mode selected but SMTP configuration is incomplete. Email to {Recipient} was not sent.",
                toEmail
            );
            return;
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_settings.SenderName, _settings.Sender));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        email.Body = new BodyBuilder { HtmlBody = message }.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            SecureSocketOptions securityOption =
                _settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await smtp.ConnectAsync(_settings.SmtpServer, _settings.Port, securityOption);
            await smtp.AuthenticateAsync(_settings.Username, _settings.Password);
            await smtp.SendAsync(email);
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(ex, "SMTP command error while sending email to {Recipient}", toEmail);
            throw;
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol error while sending email to {Recipient}", toEmail);
            throw;
        }
        catch (SmtpAuthException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed while sending email to {Recipient}", toEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while sending email to {Recipient}", toEmail);
            throw;
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}
