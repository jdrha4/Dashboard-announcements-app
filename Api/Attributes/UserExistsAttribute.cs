using System.Security.Claims;
using Application.Api.Extensions;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Application.Api.Attributes;

/// <summary>
/// Authorization attribute that ensures the current user exists in the database
/// and optionally validates that the user has one of the required roles.
///
/// This is a custom alternative to the Authorize attribute, which only checks the
/// cookie validity, without the database check.
/// </summary>
public class UserExistsAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    /// <summary>
    /// The allowed user roles. If empty, role validation is skipped, otherwise
    /// the validation will only succeed if the user has one of these roles.
    /// </summary>
    public new UserRole[] Roles { get; set; } = Array.Empty<UserRole>();

    /// <summary>
    /// Called during the authorization process. Validates that the user exists in the database
    /// and optionally verifies that the user's role matches one of the allowed roles.
    /// </summary>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        HttpContext httpContext = context.HttpContext;
        var dbContext = httpContext.RequestServices.GetRequiredService<DatabaseContext>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<UserExistsAttribute>>();
        Guid? userId = httpContext.User.GetId();

        // No user found or invalid JWT
        if (userId == null || !dbContext.Users.Any(u => u.Id == userId))
        {
            logger.LogWarning("Authentication failed: JWT cookie with non-existent user");
            httpContext.Response.Cookies.Delete("Authentication");
            context.Result = new ChallengeResult();
            return;
        }

        // Optional role check
        if (Roles.Length > 0)
        {
            string? roleClaim = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!Enum.TryParse<UserRole>(roleClaim, out UserRole userRole) || !Roles.Contains(userRole))
            {
                logger.LogWarning("Authentication failed: Role requirement ({UserRole}) wasn't met", roleClaim);
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
