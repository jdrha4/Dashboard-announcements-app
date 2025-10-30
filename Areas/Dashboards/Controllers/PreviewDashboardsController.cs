using Application.Areas.Announcements.Models;
using Application.Areas.Dashboards.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Application.Areas.Dashboards.Controllers;

[Area("Dashboards")]
[Route("dashboards/preview")]
public class PreviewDashboardsController : Controller
{
    private readonly DatabaseContext _db;
    private readonly ILogger<PreviewDashboardsController> _logger;

    public PreviewDashboardsController(DatabaseContext databaseContext, ILogger<PreviewDashboardsController> logger)
    {
        _db = databaseContext;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View("Pin");

    [HttpPost]
    public async Task<IActionResult> SubmitPin(string pin)
    {
        DashboardPreviewPin? pinEntry = await _db.DashboardPreviewPins.FirstOrDefaultAsync(p =>
            p.Pin == pin && p.Expiration > DateTime.UtcNow
        );

        if (pinEntry == null)
        {
            _logger.LogDebug("Invalid or expired PIN.");
            ViewBag.Error = "Invalid or expired PIN.";
            return View("Pin");
        }

        // Load the dashboard using the DashboardId
        Dashboard? dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == pinEntry.DashboardId);

        if (dashboard == null)
        {
            _logger.LogDebug("Can't find the dashboard to redirect to");
            ViewBag.Error = "Dashboard not found.";
            return View("Pin");
        }

        // Remove the PIN from the database
        _db.DashboardPreviewPins.Remove(pinEntry);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(ViewPublicDashboard), new { token = dashboard.DashboardToken });
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> ViewPublicDashboard(Guid token)
    {
        Dashboard? dashboard = await _db
            .Dashboards.Include(d => d.Author)
            .Include(d => d.Announcements)
            .ThenInclude(a => a.User)
            .Include(d => d.DashboardAnnouncements) // include global announcements
            .ThenInclude(da => da.Announcement)
            .FirstOrDefaultAsync(d => d.DashboardToken == token);

        if (dashboard == null)
            return NotFound("Dashboard not found.");

        // Combine the direct announcements with the global ones
        var allAnnouncements = dashboard
            .Announcements.Union(dashboard.DashboardAnnouncements.Select(da => da.Announcement))
            .OrderByDescending(a => a.CreatedAt) // Order all announcements together
            .ToList();

        var viewModel = new DashboardDetailsViewModel
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
            AuthorName = dashboard.Author.Name,
            AuthorId = dashboard.AuthorId,
            CurrentUserId = null,
            CreatedAt = dashboard.CreatedAt,
            DashboardToken = dashboard.DashboardToken,
            Announcements = allAnnouncements
                .Select(a => new AnnouncementViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    Category = a.Category,
                    UserName = a.User.Name,
                    CreatedAt = a.CreatedAt,
                    ProfileImageBase64 = a.User.ProfileImageBase64 ?? "",
                    IsImportant = a.IsImportant,
                    HasPoll = a.IsPoll,
                })
                .ToList(),
        };

        // Full URL passed to View for QR code generation
        string fullDashboardUrl = Url.Action(
            "Details",
            "UserDashboards",
            new { dashboardId = dashboard.Id },
            Request.Scheme
        )!;
        ViewBag.DashboardLink = fullDashboardUrl;

        return View("PublicDashboard", viewModel);
    }

    [HttpGet("htmx/{dashboardId}")]
    public IActionResult GetAnnouncements(Guid dashboardId, Guid dashboardToken)
    {
        Dashboard? dashboard = _db
            .Dashboards.Include(d => d.Author)
            .Include(d => d.Announcements)
            .ThenInclude(a => a.User)
            .Include(d => d.DashboardAnnouncements) // include global announcements
            .ThenInclude(da => da.Announcement)
            .FirstOrDefault(d => d.Id == dashboardId && d.DashboardToken == dashboardToken);

        if (dashboard == null)
            return NotFound();

        // Combine the direct announcements with the global ones
        var allAnnouncements = dashboard
            .Announcements.Union(dashboard.DashboardAnnouncements.Select(da => da.Announcement))
            .OrderByDescending(a => a.CreatedAt) // Order all announcements together
            .ToList();

        var announcements = allAnnouncements
            .Select(a => new AnnouncementViewModel
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                Category = a.Category,
                UserName = a.User.Name,
                ProfileImageBase64 = a.User.ProfileImageBase64 ?? string.Empty,
                CreatedAt = a.CreatedAt,
                IsImportant = a.IsImportant,
            })
            .ToList();

        return PartialView("_AnnouncementsList", announcements);
    }
}
