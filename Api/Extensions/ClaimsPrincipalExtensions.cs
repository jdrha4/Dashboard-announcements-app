using System.Security.Claims;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Retrieves the user ID from the claims principal, if available.
    /// </summary>
    /// <returns>The user ID as a Guid, or null if not found or invalid.</returns>
    public static Guid? GetId(this ClaimsPrincipal claimsPrincipal)
    {
        Claim? idClaim = claimsPrincipal.FindFirst("Id");
        if (idClaim == null)
            return null;

        return Guid.TryParse(idClaim.Value, out Guid id) ? id : null;
    }

    /// <summary>
    /// Retrieves the user ID from the claims principal or throws if missing.
    /// </summary>
    /// <returns>The user ID as a Guid.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user ID is missing or invalid.</exception>
    public static Guid GetRequiredId(this ClaimsPrincipal claimsPrincipal)
    {
        Guid? userId = GetId(claimsPrincipal);
        if (userId != null)
            return (Guid)userId;

        throw new UnauthorizedAccessException("User ID is missing.");
    }

    /// <summary>
    /// Retrieves the user entity from the database based on the claims principal.
    /// </summary>
    /// <returns>The user entity, or null if not found.</returns>
    public static UserDo? GetUser(this ClaimsPrincipal claimsPrincipal, DatabaseContext dbContext)
    {
        Guid? userId = claimsPrincipal.GetId();
        if (userId == null)
            return null;

        return dbContext.Users.FirstOrDefault(u => u.Id == userId);
    }

    /// <summary>
    /// Retrieves the user entity from the database or throws if not found.
    /// </summary>
    /// <returns>The user entity.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not found.</exception>
    public static UserDo GetRequiredUser(this ClaimsPrincipal claimsPrincipal, DatabaseContext dbContext)
    {
        Guid userId = GetRequiredId(claimsPrincipal);
        UserDo? user = dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            throw new UnauthorizedAccessException("No user with given ID");

        return user;
    }

    /// <summary>
    /// Checks whether the user has access to the specified dashboard (view or add announcements).
    /// </summary>
    /// <returns>True if the user has access; otherwise, false.</returns>
    public static bool HasDashboardAccess(
        this ClaimsPrincipal claimsPrincipal,
        Guid dashboardId,
        DatabaseContext dbContext
    )
    {
        Guid? userId = claimsPrincipal.GetId();
        if (userId == null)
            return false;

        bool isAdmin = claimsPrincipal.IsInRole("Admin");

        return isAdmin
            || dbContext
                .Dashboards.Where(d => d.Id == dashboardId)
                .Include(d => d.UserDashboards)
                .Any(d => d.AuthorId == userId || d.UserDashboards.Any(ud => ud.UserId == userId));
    }

    /// <summary>
    /// Checks whether the user has management rights (ownership or admin) over the specified dashboard.
    /// </summary>
    /// <returns>True if the user can manage the dashboard; otherwise, false.</returns>
    public static bool HasManageDashboardAccess(
        this ClaimsPrincipal claimsPrincipal,
        Guid dashboardId,
        DatabaseContext dbContext
    )
    {
        Guid? userId = claimsPrincipal.GetId();
        if (userId == null)
            return false;

        bool isAdmin = claimsPrincipal.IsInRole("Admin");

        return isAdmin || dbContext.Dashboards.Where(d => d.Id == dashboardId).Any(d => d.AuthorId == userId);
    }

    /// <summary>
    /// Determines whether the user has access to the specified announcement.
    /// Access is granted if the user is an admin, or the author of the announcement,
    /// or the owner of the dashboard that owns the announcement.
    /// /// </summary>
    public static bool HasAnnouncementAccess(this ClaimsPrincipal user, Announcement announcement, DatabaseContext db)
    {
        Guid userId = user.GetRequiredId();
        bool isAdmin = user.IsInRole("Admin");
        bool isAuthor = announcement.UserId == userId;
        bool isDashboardOwner = announcement.Dashboard?.AuthorId == userId;

        return isAdmin || isAuthor || isDashboardOwner;
    }
}
