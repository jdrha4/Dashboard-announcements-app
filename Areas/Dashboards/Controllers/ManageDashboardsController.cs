using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.Dashboards.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DbModels = Application.Infrastructure.Database.Models;

namespace Application.Areas.Dashboards.Controllers;

[Area("Dashboards")]
[Route("dashboards/manage")]
[UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
public class ManageDashboardsController : Controller
{
    private readonly DatabaseContext _db;
    private readonly ILogger<ManageDashboardsController> _logger;

    public ManageDashboardsController(DatabaseContext databaseContext, ILogger<ManageDashboardsController> logger)
    {
        _db = databaseContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        Guid currentUserId = User.GetRequiredId();
        bool isAdmin = User.IsInRole("Admin");

        IQueryable<Dashboard> dashboardsQuery = _db
            .Dashboards.Include(d => d.Author)
            .Include(d => d.Announcements)
            .Include(d => d.DashboardAnnouncements)
            .ThenInclude(da => da.Announcement) // Include the global announcements
            .AsQueryable();

        if (!isAdmin)
            dashboardsQuery = dashboardsQuery.Where(d => d.AuthorId == currentUserId);

        List<DashboardViewModel> dashboards = await dashboardsQuery
            .Select(d => new DashboardViewModel
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                AuthorName = d.Author.Name,
                AnnouncementCount = d.Announcements.Count + d.DashboardAnnouncements.Count,
                ProfileImageBase64 = d.Author.ProfileImageBase64 ?? string.Empty,
            })
            .ToListAsync();

        ViewData["Title"] = User.IsInRole("Admin") ? "Manage All Dashboards" : "Manage My Dashboards";
        return View("List", dashboards);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View("Create", new CreateDashboardViewModel());
    }

    [HttpPost("htmx/create")]
    public async Task<IActionResult> CreateSubmit(CreateDashboardViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_CreateForm", model);

        Guid authorId = User.GetRequiredId();

        bool nameExists = await _db.Dashboards.AnyAsync(d => d.Name == model.Name);
        if (nameExists)
        {
            _logger.LogDebug("Failed to create a new dashboard: name already exists");
            ModelState.AddModelError(nameof(model.Name), "A dashboard with this name already exists.");
            return PartialView("_CreateForm", model);
        }

        var dashboard = new DbModels.Dashboard
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            AuthorId = authorId,
        };

        await _db.AddAsync(dashboard);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} created a new dashboard: {DashboardName} ({DashboardId})",
            authorId,
            dashboard.Name,
            dashboard.Id
        );

        TempData["SuccessMessage"] = "Dashboard created successfully.";
        Response.Htmx(h => h.Redirect(Url.Action(nameof(Index))!));
        return Ok();
    }

    [HttpGet("{dashboardId}/assign-users")]
    public IActionResult AssignUsers(Guid dashboardId)
    {
        Dashboard? dashboard = _db
            .Dashboards.Include(d => d.UserDashboards)
            .ThenInclude(ud => ud.User)
            .Include(d => d.Announcements)
            .Include(d => d.DashboardAnnouncements)
            .ThenInclude(da => da.Announcement) // Include global announcements
            .FirstOrDefault(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogTrace("Attempted to visit the assign users page for a non-existent dashboard");
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to access the assign-users page for dashboard {DashboardId} which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        List<UserDo> users = _db.Users.ToList();

        var model = new DashboardViewModel
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
            AuthorName = dashboard.Author.Name,
            AnnouncementCount = dashboard.Announcements.Count + dashboard.DashboardAnnouncements.Count,
            ProfileImageBase64 = dashboard.Author.ProfileImageBase64 ?? string.Empty,
        };

        ViewBag.Users = users;

        return View("AssignUsers", model);
    }

    [HttpPost("htmx/{dashboardId}/assign")]
    public async Task<IActionResult> AssignUser(Guid dashboardId, [FromForm] Guid userId)
    {
        Dashboard? dashboard = await _db
            .Dashboards.Include(d => d.UserDashboards)
            .FirstOrDefaultAsync(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogWarning("Attempted to assign a user to a non-existent dashboard ({DashboardId})", dashboardId);
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to assign a user ({TargetUserId}) to dashboard {DashboardId} which they don't have access to",
                currentUserId,
                userId,
                dashboardId
            );
            return Forbid();
        }

        bool userExists = await _db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            _logger.LogWarning(
                "User {UserId} Attempted to assign a non-existent user ({TargetUserId}) to a dashboard {DashboardId}",
                currentUserId,
                userId,
                dashboardId
            );
            return NotFound("User not found.");
        }

        if (dashboard.UserDashboards.Any(ud => ud.UserId == userId))
        {
            _logger.LogDebug(
                "Attempted to assign an already-assigned user ({TargetUserId}) to a dashboard {DashboardId}",
                userId,
                dashboardId
            );
            Response.Htmx(h => h.WithTrigger("sendToast", new { msg = "User already assigned", bg = "danger" }));
            return Ok("User already assigned");
        }

        dashboard.UserDashboards.Add(new UserDashboardMap { DashboardId = dashboardId, UserId = userId });
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} assigned user {TargetUserId} to dashboard {DashboardId}",
            currentUserId,
            userId,
            dashboardId
        );

        Response.Htmx(h =>
            h.WithTrigger("users-updated").WithTrigger("sendToast", new { msg = "User assigned", bg = "success" })
        );
        return Ok();
    }

    [HttpDelete("htmx/{dashboardId}/assign")]
    public async Task<IActionResult> UnassignUser(Guid dashboardId, [FromQuery] Guid userId)
    {
        Dashboard? dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogWarning("Attempted to unassign a user to a non-existent dashboard ({DashboardId})", dashboardId);
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to unassign user ({TargetUserId}) from dashboard {DashboardId} which they don't have access to",
                currentUserId,
                userId,
                dashboardId
            );
            return Forbid();
        }

        UserDashboardMap? assignment = await _db.UserDashboards.FirstOrDefaultAsync(ud =>
            ud.DashboardId == dashboardId && ud.UserId == userId
        );

        if (assignment == null)
        {
            _logger.LogDebug(
                "Attempted to unassign a user ({TargetUserId}) from dashboard {DashboardId}, but this user wasn't assigned to it",
                userId,
                dashboardId
            );
            Response.Htmx(h =>
                h.WithTrigger("sendToast", new { msg = "User is not assigned to this dashboard", bg = "danger" })
            );
            return Ok("User is not assigned to this dashboard");
        }

        _db.UserDashboards.Remove(assignment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} unassigned user {TargetUserId} from dashboard {DashboardId}",
            currentUserId,
            userId,
            dashboardId
        );

        Response.Htmx(h =>
            h.WithTrigger("users-updated").WithTrigger("sendToast", new { msg = "User unassigned", bg = "warning" })
        );
        return Ok();
    }

    [HttpGet("htmx/{dashboardId}/assigned")]
    public async Task<IActionResult> Assignedusers(Guid dashboardId)
    {
        Dashboard? dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogWarning(
                "Attempted to access the assigned users of a non-existent dashboard ({DashboardId})",
                dashboardId
            );
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to access the assigned users page for dashboard {DashboardId} which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        List<AssignUsersViewModel.UserSelection> assignedUsers = await _db
            .UserDashboards.Where(ud => ud.DashboardId == dashboardId)
            .OrderBy(ud => ud.User.Name)
            .Select(ud => new AssignUsersViewModel.UserSelection
            {
                UserId = ud.User.Id,
                UserName = ud.User.Name,
                UserEmail = ud.User.Email,
                IsAssigned = true,
            })
            .AsNoTracking()
            .ToListAsync();

        var model = new AssignUsersViewModel
        {
            DashboardId = dashboardId,
            DashboardName = dashboard.Name,
            Users = assignedUsers,
        };

        return PartialView("_AssignedUsersList", model);
    }

    [HttpGet("htmx/{dashboardId}/search-users")]
    public async Task<IActionResult> SearchUsers(Guid dashboardId, string query)
    {
        Dashboard? dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogWarning(
                "Attempted to access the search-users of a non-existent dashboard ({DashboardId})",
                dashboardId
            );
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to access the search-users page for dashboard {DashboardId} which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        List<AssignUsersViewModel.UserSelection> users = await _db
            .Users.Where(u => u.Name.Contains(query) || u.Email.Contains(query))
            .OrderBy(u => u.Name)
            .Take(10)
            .Select(u => new AssignUsersViewModel.UserSelection
            {
                UserId = u.Id,
                UserName = u.Name,
                UserEmail = u.Email,
                IsAssigned = u.UserDashboards.Any(ud => ud.DashboardId == dashboardId),
            })
            .AsNoTracking()
            .ToListAsync();

        var model = new AssignUsersViewModel
        {
            DashboardId = dashboardId,
            DashboardName = dashboard.Name,
            Users = users,
        };

        return PartialView("_UserSearchResults", model);
    }

    [HttpGet("{dashboardId}/edit")]
    public async Task<IActionResult> Edit(Guid dashboardId)
    {
        Dashboard? dashboard = await _db.Dashboards.FindAsync(dashboardId);
        if (dashboard == null)
        {
            _logger.LogWarning(
                "Attempted to access the edit page of a non-existent dashboard ({DashboardId})",
                dashboardId
            );
            return NotFound("This dashboard doesn't exist");
        }

        var currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to access edit page for dashboard {DashboardId}, which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        var vm = new EditDashboardViewModel
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
        };

        return View("EditDashboard", vm);
    }

    [HttpPut("htmx/{dashboardId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubmit(Guid dashboardId, EditDashboardViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_EditDashboardForm", model);

        Dashboard? dashboard = await _db.Dashboards.FindAsync(dashboardId);
        if (dashboard == null)
        {
            _logger.LogWarning("Attempted to access edit a non-existent dashboard ({DashboardId})", dashboardId);
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to edit dashboard {DashboardId}, which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        dashboard.Name = model.Name.Trim();
        dashboard.Description = model.Description?.Trim();

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Dashboard updated successfully.";
        Response.Htmx(h => h.Redirect(Url.Action("Details", "UserDashboards", new { dashboardId = dashboard.Id })!));

        _logger.LogInformation(
            "User {UserId} edited a dashboard: {DashboardName} ({DashboardId})",
            currentUserId,
            dashboard.Name,
            dashboard.Id
        );

        return Ok();
    }

    [HttpPost("htmx/{dashboardId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid dashboardId)
    {
        Dashboard? dashboard = await _db
            .Dashboards.Include(d => d.Announcements)
            .FirstOrDefaultAsync(d => d.Id == dashboardId);

        if (dashboard == null)
        {
            _logger.LogWarning("Attempted to delete a non-existent dashboard ({DashboardId})", dashboardId);
            return NotFound("This dashboard doesn't exist");
        }

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete dashboard {DashboardId}, which they don't have access to",
                currentUserId,
                dashboardId
            );
            return Forbid();
        }

        // Handle global announcement linked to this dashboard,
        // making sure to delete any orphaned global announcements as well
        List<DashboardAnnouncementMap> mapsToDelete = await _db
            .DashboardAnnouncements.Where(m => m.DashboardId == dashboardId)
            .ToListAsync();

        List<Guid> globalAnnouncementIds = mapsToDelete.Select(m => m.AnnouncementId).Distinct().ToList();

        _db.DashboardAnnouncements.RemoveRange(mapsToDelete);
        await _db.SaveChangesAsync();

        List<Guid> orphanedAnnouncementIds = await _db
            .DashboardAnnouncements.Where(m => globalAnnouncementIds.Contains(m.AnnouncementId))
            .GroupBy(m => m.AnnouncementId)
            .Where(g => !g.Any()) // no mappings remaining
            .Select(g => g.Key)
            .ToListAsync();

        if (orphanedAnnouncementIds.Count > 0)
        {
            List<Announcement> orphanedAnnouncements = await _db
                .Announcements.Where(a => orphanedAnnouncementIds.Contains(a.Id))
                .ToListAsync();

            _db.Announcements.RemoveRange(orphanedAnnouncements);
            await _db.SaveChangesAsync();
        }

        _db.Dashboards.Remove(dashboard);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Dashboard deleted successfully.";
        Response.Htmx(h => h.Redirect(Url.Action(nameof(Index))!));

        _logger.LogInformation(
            "User {UserId} deleted a dashboard: {DashboardName} ({DashboardId})",
            currentUserId,
            dashboard.Name,
            dashboard.Id
        );

        return Ok();
    }

    [HttpPost("{dashboardId}/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettings(Guid dashboardId, int expiryOption, int maxAnnouncements)
    {
        var dash = await _db.Dashboards.FindAsync(dashboardId);
        if (dash == null)
            return NotFound();

        Guid currentUserId = User.GetRequiredId();

        if (!User.HasManageDashboardAccess(dashboardId, _db))
            return Forbid();

        dash.ExpiryOption = (ExpiryOption)expiryOption;
        dash.MaxAnnouncements = maxAnnouncements;
        await _db.SaveChangesAsync();

        Response.Htmx(h => h.WithTrigger("sendToast", new { msg = "Settings saved", bg = "success" }));
        return Ok();
    }
}
