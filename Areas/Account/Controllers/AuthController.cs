using System.Security.Claims;
using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.Account.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Application.Services;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.Account.Controllers;

[Area("Account")]
[Route("auth")]
public class AuthController : Controller
{
    private readonly DatabaseContext _db;
    private readonly AccountService _accountService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        DatabaseContext databaseContext,
        AccountService accountService,
        ILogger<AuthController> logger
    )
    {
        _db = databaseContext;
        _accountService = accountService;
        _logger = logger;
    }

    [UserExists]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoute("default");

        return RedirectToAction(nameof(Login));
    }

    /// Renders the login page. If the user is already authenticated,
    /// redirects them to their intended return URL or the default route.
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && !Url.IsLocalUrl(returnUrl))
        {
            // redirecting to non-local URLs from login is not supported
            // and looks like a malicious attempt at something, remove
            // this redirect param.
            _logger.LogWarning("Removing external post-login redirect to: {Url}", returnUrl);
            returnUrl = null;
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            _logger.LogTrace("User already logged in");

            if (string.IsNullOrEmpty(returnUrl))
                return RedirectToRoute("default");

            return LocalRedirect(returnUrl);
        }

        return View("Login", new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// Handles registration form submission via HTMX.
    /// If email confirmation is enabled, triggers confirmation workflow.
    /// Otherwise, logs in user immediately.
    [HttpPost("htmx/login")]
    public async Task<IActionResult> LoginSubmit(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_LoginForm", model);

        UserDo? user = await _accountService.AuthenticateUserAsync(model.Email, model.Password);
        if (user == null)
        {
            Response.Htmx(h => h.WithTrigger("sendToast", new { msg = "Invalid credentials", bg = "danger" }));
            return PartialView("_LoginForm", model);
        }

        if (!await _accountService.IsUserConfirmedAsync(user.Id))
        {
            Response.Htmx(h =>
                h.WithTrigger("sendToast", new { msg = "Please confirm your email before logging in.", bg = "warning" })
            );
            return PartialView("_LoginForm", model);
        }

        await _accountService.SignInUserAsync(HttpContext, user);
        _logger.LogInformation("User {Email} ({UserId}) logged in sucessfully", user.Email, user.Id);

        // We trust that this redirect is safe, the /login GET should've cleaned it.
        // Even if not, this endpoint can't really be sent to someone as a URL maliciously
        // with this redirect embedded and it requires login credentials in the request,
        // so we don't need to care if we can trust this redirect or not.
        string redirectUrl = string.IsNullOrEmpty(model.ReturnUrl) ? Url.RouteUrl("default")! : model.ReturnUrl;
        Response.Htmx(h => h.Redirect(redirectUrl));

        return Ok();
    }

    [HttpGet("register")]
    public IActionResult Register() => View("Register", new RegisterViewModel());

    [HttpGet("register-success")]
    public IActionResult RegisterSuccess() => View("RegisterSuccess");

    [HttpPost("htmx/register")]
    public async Task<IActionResult> RegisterSubmit(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_RegisterForm", model);

        if (!await _accountService.EmailIsAllowedAsync(model.Email))
        {
            string? userDomain = model.Email.Split('@')[1].ToLowerInvariant();

            ModelState.AddModelError(nameof(model.Email), "Registration is only allowed for specific email domains.");
            return PartialView("_RegisterForm", model);
        }

        if (await _accountService.EmailExistsAsync(model.Email))
        {
            _logger.LogTrace("Registration attempt for account {Email} failed (email already registered)", model.Email);
            Response.Htmx(h =>
                h.WithTrigger("sendToast", new { msg = "This email is already registered.", bg = "danger" })
            );
            return PartialView("_RegisterForm", model);
        }

        UserDo newUser = await _accountService.RegisterUserAsync(model.Email, model.Name, model.Password);
        _logger.LogInformation("User {Email} ({UserId}) was registered", newUser.Email, newUser.Id);

        Response.Htmx(h => h.Redirect(Url.Action(nameof(RegisterSuccess))!));

        if (_accountService.IsConfirmationEnabled)
        {
            await _accountService.SendConfirmationEmailAsync(
                newUser,
                // Generate an absolute URL to the confirm email endpoint
                token => Url.Action(nameof(ConfirmEmail), null, new { token }, Request.Scheme)!
            );
        }

        return Ok();
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await _accountService.signOutUserAsync(HttpContext);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View("ForgotPassword");

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordSubmit(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View("ForgotPassword", model);

        UserDo? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (user == null)
        {
            _logger.LogWarning("Requested password reset for a non-existent email: {Email}", model.Email);
            // Show a success page anyways, the user doesn't need to be informed that the email wasn't
            // registered, this avoids leaking that information.
            return View("ForgotPasswordConfirmation");
        }

        await _accountService.SendPasswordResetEmailAsync(
            user,
            // Generate an absolute URL to the confirm email endpoint
            token => Url.Action(nameof(ResetPassword), null, new { token }, Request.Scheme)!
        );

        return View("ForgotPasswordConfirmation");
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string token)
    {
        UserDo? user = await _accountService.ConfirmEmailAsync(token);
        if (user == null)
            return View("ConfirmEmailInvalid");

        return View("ConfirmEmailSuccess");
    }

    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string token)
    {
        UserDo? user = await _accountService.CheckResetTokenAsync(token);
        if (user == null)
        {
            _logger.LogWarning("Reset password visited with an invalid / expired token");
            return Forbid();
        }

        return View("ResetPassword", new ResetPasswordViewModel { Token = token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordSubmit(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View("ResetPassword", model);

        UserDo? user = await _accountService.CheckResetTokenAsync(model.Token);
        if (user == null)
        {
            _logger.LogWarning("Attempted a password reset with an invalid / expired token");
            ModelState.AddModelError("", "Invalid or expired token.");
            return View("ResetPassword", model);
        }

        await _accountService.ResetUserPasswordAsync(user, model.NewPassword);

        _logger.LogWarning("The password for user {Email} ({UserId}) was reset", user.Email, user.Id);

        TempData["SuccessMessage"] = "Your password has been reset successfully.";
        return View("ResetPasswordConfirmation");
    }
}
