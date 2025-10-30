using System.Security.Claims;
using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.Account.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Application.Services;
using Htmx;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.Account.Controllers;

[Area("Account")]
[Route("profile")]
public class ProfileController : Controller
{
    private readonly DatabaseContext _db;
    private readonly AccountService _accountService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        DatabaseContext databaseContext,
        AccountService accountService,
        ILogger<ProfileController> logger
    )
    {
        _db = databaseContext;
        _accountService = accountService;
        _logger = logger;
    }

    [UserExists]
    public IActionResult Index()
    {
        UserDo user = User.GetRequiredUser(_db);

        return View(
            "Profile",
            new AccountViewModel
            {
                Email = user.Email,
                Name = user.Name,
                ProfileImageBase64 = user.ProfileImageBase64,
            }
        );
    }

    [HttpGet("edit")]
    [UserExists]
    public IActionResult Edit()
    {
        UserDo user = User.GetRequiredUser(_db);
        return View(
            "Edit",
            new EditAccountViewModel
            {
                Email = user.Email,
                Name = user.Name,
                ProfileImageBase64 = user.ProfileImageBase64,
            }
        );
    }

    [HttpPost("htmx/edit")]
    [UserExists]
    public async Task<IActionResult> EditSubmit(EditAccountViewModel model)
    {
        UserDo user = User.GetRequiredUser(_db);
        model.ProfileImageBase64 = user.ProfileImageBase64;

        if (!ModelState.IsValid)
            return PartialView("_EditForm", model);

        if (!_accountService.ValidatePasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Incorrect current password.");
            return PartialView("_EditForm", model);
        }

        bool nameUnchanged = model.Name == user.Name;
        bool emailUnchanged = model.Email == user.Email;
        bool noNewImage = !model.RemoveProfileImage && (model.ProfileImage == null || model.ProfileImage.Length == 0);

        if (nameUnchanged && emailUnchanged && noNewImage)
        {
            Response.Htmx(h => h.WithTrigger("sendToast", new { msg = "No changes were made.", bg = "info" }));
            return PartialView("_EditForm", model);
        }

        string? finalImageB64 = user.ProfileImageBase64;
        if (model.RemoveProfileImage)
        {
            finalImageB64 = null;
        }
        else if (model.ProfileImage?.Length > 0)
        {
            if (!IsValidProfileImage(model.ProfileImage, out string? error))
            {
                Response.Htmx(h => h.WithTrigger("sendToast", new { msg = error, bg = "danger" }));
                return PartialView("_EditForm", model);
            }
            finalImageB64 = await ConvertImageToBase64Async(model.ProfileImage);
        }

        await _accountService.UpdateUserAsync(user, model.Name, model.Email, finalImageB64);
        await _accountService.SignInUserAsync(HttpContext, user);

        Response.Htmx(h =>
        {
            h.Redirect(Url.Action(nameof(Index))!);
            h.WithTrigger("sendToast", new { msg = "Profile updated successfully.", bg = "success" });
        });
        return Ok();
    }

    [HttpGet("change-password")]
    [UserExists]
    public IActionResult ChangePassword() => View("ChangePassword", new ChangePasswordViewModel());

    [HttpPost("htmx/change-password")]
    [UserExists]
    public async Task<IActionResult> ChangePasswordSubmit(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_ChangePasswordForm", model);

        UserDo user = User.GetRequiredUser(_db);

        if (!_accountService.ValidatePasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Incorrect current password.");
            return PartialView("_ChangePasswordForm", model);
        }

        await _accountService.ChangeUserPasswordAsync(user, model.NewPassword);

        Response.Htmx(h => h.Redirect(Url.Action(nameof(Index))!));
        return Ok();
    }

    [HttpPost("htmx/delete")]
    [ValidateAntiForgeryToken]
    [UserExists]
    public async Task<IActionResult> DeleteOwnAccount()
    {
        UserDo user = User.GetRequiredUser(_db);

        bool isLastAdmin =
            user.Role == UserRole.Admin && !_db.Users.Any(u => u.Role == UserRole.Admin && u.Id != user.Id);

        if (isLastAdmin)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete their account but is the last administrator.",
                user.Id
            );

            Response.Htmx(h =>
                h.WithTrigger(
                    "sendToast",
                    new
                    {
                        msg = "You cannot delete the last administrator.",
                        bg = "danger",
                        position = "top-end",
                    }
                )
            );

            return BadRequest("You cannot delete the last administrator");
        }

        _logger.LogInformation("User {UserId} deleted their account.", user.Id);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Account deletion completed and user signed out: {UserId}", user.Id);

        await HttpContext.SignOutAsync();

        Response.Htmx(h =>
        {
            h.WithTrigger(
                "sendToast",
                new
                {
                    msg = "Your account was successfully deleted.",
                    bg = "success",
                    position = "top-end",
                }
            );
            h.Redirect("/");
        });

        return Ok();
    }

    /// <summary>
    /// Validates that the uploaded file is an acceptable profile image.
    /// </summary>
    /// <param name="file">The uploaded form file to validate.</param>
    /// <param name="error">
    /// If validation fails, contains a message describing why; otherwise null.
    /// </param>
    /// <returns>
    /// True if the image meets all requirements; otherwise false.
    /// </returns>
    private static bool IsValidProfileImage(IFormFile file, out string? error)
    {
        const int MaxFileSize = 2 * 1024 * 1024;
        string[] allowedTypes = ["image/jpeg", "image/png", "image/gif"];

        if (file.Length > MaxFileSize)
        {
            error = "The image is too large. Maximum allowed size is 2MB.";
            return false;
        }

        string contentType = file.ContentType.ToLowerInvariant();
        if (!allowedTypes.Contains(contentType))
        {
            error = "Only JPG, PNG, and GIF formats are allowed.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Converts a form file into a base64-encoded data URI image string.
    /// </summary>
    /// <remarks>
    /// Assumes the input has already been validated as an image.
    /// See <see cref="IsValidProfileImage"/>.
    /// </remarks>
    /// <param name="image">The uploaded image file.</param>
    /// <returns>The base64-encoded data URI string.</returns>
    private static async Task<string> ConvertImageToBase64Async(IFormFile image)
    {
        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);
        string base64 = Convert.ToBase64String(memoryStream.ToArray());
        string contentType = image.ContentType.ToLowerInvariant();
        return $"data:{contentType};base64,{base64}";
    }
}
