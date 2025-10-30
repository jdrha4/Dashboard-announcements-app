using System.Globalization;
using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.Account.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Application.Areas.Account.Controllers;

[Area("Account")]
[Route("account/user-roles")]
[UserExists(Roles = new[] { UserRole.Admin })]
public class UserRolesController : Controller
{
    private readonly DatabaseContext _db;
    private readonly ILogger<UserRolesController> _logger;

    public UserRolesController(DatabaseContext db, ILogger<UserRolesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(string? query = null)
    {
        UserDo loggedUser = User.GetRequiredUser(_db);

        // Get at most 15 users that match the search query.
        // If users want to see more, they should enter a more specific query.
        // TODO: This could benefit from adding an infinite scroll logic
        List<UserRoleViewModel> users = _db
            .Users.Where(u => string.IsNullOrWhiteSpace(query) || u.Name.Contains(query) || u.Email.Contains(query))
            .OrderBy(u => u.Name)
            .ToList()
            .Take(15)
            .Select(u => new UserRoleViewModel
            {
                UserId = u.Id,
                UserName = u.Name,
                UserEmail = u.Email,
                CurrentRole = u.Role,
                IsSelf = u.Id == loggedUser.Id,
                ProfileImageBase64 = u.ProfileImageBase64 ?? string.Empty,
            })
            .ToList();

        // Populate the roles dropdown for role changes.
        ViewData["Roles"] = Enum.GetValues<UserRole>()
            .Select(r => new SelectListItem
            {
                Text = r.ToString(),
                Value = ((int)r).ToString(CultureInfo.InvariantCulture),
            })
            .ToList();

        if (Request.Headers["HX-Request"] == "true")
            return PartialView("_UserList", users);
        return View("ViewRoles", users);
    }

    [HttpPost("htmx/change-role")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeRole(Guid userId, UserRole newRole)
    {
        UserDo loggedUser = User.GetRequiredUser(_db);
        UserDo? user = _db.Users.FirstOrDefault(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} wasn't found, role change to {NewRole} failed", userId, newRole);
            return NotFound("User not found.");
        }

        if (user.Id == loggedUser.Id)
        {
            _logger.LogWarning(
                "Role change of user {Email} ({UserId}) to {NewRole} failed: refusing self-role change",
                user.Email,
                userId,
                newRole
            );
            return Forbid();
        }

        UserRole oldRole = user.Role;
        user.Role = newRole;
        _db.SaveChanges();

        _logger.LogInformation(
            "User {Email} ({UserId}) changed the role of {TargetEmail} ({TargetUserId}) {OldRole}->{NewRole}",
            loggedUser.Email,
            loggedUser.Id,
            user.Email,
            user.Id,
            oldRole,
            newRole
        );
        var updatedViewModel = new UserRoleViewModel
        {
            UserId = user.Id,
            UserName = user.Name,
            UserEmail = user.Email,
            CurrentRole = user.Role,
            IsSelf = user.Id == loggedUser.Id,
            ProfileImageBase64 = user.ProfileImageBase64 ?? string.Empty,
        };

        // Update the roles dropdown after the change.
        ViewData["Roles"] = Enum.GetValues<UserRole>()
            .Select(r => new SelectListItem
            {
                Text = r.ToString(),
                Value = ((int)r).ToString(CultureInfo.InvariantCulture),
            })
            .ToList();

        Response.Htmx(h =>
            h.WithTrigger("sendToast", new { msg = $"Updated {user.Name}'s role to {newRole}.", bg = "success" })
        );

        return PartialView("_UserRoleFormRow", updatedViewModel);
    }

    [HttpPost("htmx/delete/{userId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        Guid currentUserId = User.GetRequiredId();

        // Deleting your own account shouldn't be possible through this endpoint
        // (the option is not accessible from the UI). So we don't need to send a toast
        // here or use anything "user friendly" just return a 403
        if (userId == currentUserId)
        {
            _logger.LogWarning("User {UserId} attempted to delete their own account via admin panel.", currentUserId);
            return Forbid();
        }

        UserDo? user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Attempted to delete user {UserId}, but they were not found.", userId);
            return NotFound();
        }

        if (user.Role == UserRole.Admin && !_db.Users.Any(u => u.Role == UserRole.Admin && u.Id != userId))
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete the last remaining admin ({TargetUserId}).",
                currentUserId,
                userId
            );

            Response.Htmx(h =>
                h.WithTrigger(
                    "sendToast",
                    new
                    {
                        msg = "Cannot delete the last admin.",
                        bg = "danger",
                        position = "top-end",
                    }
                )
            );

            return BadRequest("Cannot delete the last admin.");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} ({Email}) was deleted by admin {AdminId}",
            user.Id,
            user.Email,
            currentUserId
        );

        Response.Htmx(h =>
            h.WithTrigger(
                "sendToast",
                new
                {
                    msg = $"User {user.Email} deleted.",
                    bg = "danger",
                    position = "bottom-center",
                }
            )
        );

        return Ok();
    }
}
