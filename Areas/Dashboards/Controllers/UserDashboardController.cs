using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.Account.Models;
using Application.Areas.Announcements.Models;
using Application.Areas.Dashboards.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Htmx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Application.Areas.Dashboards.Controllers
{
    [Area("Dashboards")]
    [Route("dashboards")]
    [UserExists]
    public class UserDashboardsController : Controller
    {
        private readonly DatabaseContext _db;
        private readonly ILogger<UserDashboardsController> _logger;

        private const int PIN_EXPIRATION_SECONDS = 300;

        private const int MAX_ATTEMPTS = 50;

        public UserDashboardsController(DatabaseContext databaseContext, ILogger<UserDashboardsController> logger)
        {
            _db = databaseContext;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(AnnouncementCategory? category = null)
        {
            Guid userId = User.GetRequiredId();

            List<SelectListItem> dashboardSelectList = GetDashboardSelectList(userId, null);
            ViewBag.Dashboards = dashboardSelectList;
            ViewBag.Categories = GetCategorySelectList(category);

            if (dashboardSelectList.Count == 0)
                return View("List", new List<DashboardViewModel>());

            List<Guid> accessibleDashboardIds = dashboardSelectList.Select(d => Guid.Parse(d.Value)).ToList();

            // Instead of querying announcements, query the dashboards assigned to the user.
            List<DashboardViewModel> dashboards = _db
                .Dashboards.Include(d => d.Author)
                .Include(d => d.Announcements)
                .Include(d => d.DashboardAnnouncements)
                .ThenInclude(da => da.Announcement) // Include the global announcements
                .Where(d => accessibleDashboardIds.Contains(d.Id))
                .Select(d => new DashboardViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    AuthorName = d.Author.Name,
                    AnnouncementCount = d.Announcements.Count + d.DashboardAnnouncements.Count,
                    ProfileImageBase64 = d.Author.ProfileImageBase64 ?? string.Empty,
                    ExpiryOption = (int)d.ExpiryOption,
                    MaxAnnouncements = d.MaxAnnouncements > 0 ? d.MaxAnnouncements : 50,
                })
                .ToList();

            // Return the view that expects IEnumerable<DashboardViewModel>
            return View("List", dashboards);
        }

        [HttpGet("{dashboardId}")]
        public async Task<IActionResult> Details(Guid dashboardId)
        {
            Guid userId = User.GetRequiredId();

            // TODO: Introduce pagination / infinite scroll here, as there might be
            // a lot of announcement per dashboard and we don't want to load them
            // all at once like this.
            Dashboard? dashboard = await _db
                .Dashboards.Include(d => d.Author)
                .Include(d => d.Announcements) // Directly linked announcements
                .ThenInclude(a => a.User)
                .Include(d => d.DashboardAnnouncements) // Linked through DashboardAnnouncementMap
                .ThenInclude(da => da.Announcement) // Fetch the actual announcement
                .FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dashboard == null)
            {
                _logger.LogTrace("Attempted to access a non-existent dashboard: {DashboardId}", dashboardId);
                return NotFound("This dashboard doesn't exist");
            }

            if (!User.HasDashboardAccess(dashboardId, _db))
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access a dashboard they don't have access to ({DashboardId})",
                    userId,
                    dashboardId
                );
                return Forbid();
            }

            // Prepare the redirect URL based on the referer
            string? redirect = Request.Headers.Referer.ToString();
            string defaultUrl = Url.Action("Index", "UserDashboards")!;
            _ = Uri.TryCreate(redirect, UriKind.Absolute, out Uri? parsedUri);
            string refererPath = parsedUri?.AbsolutePath ?? "";
            string[] expectedRedirects =
            {
                Url.Action("Index", "UserDashboards")!,
                Url.Action("Index", "ManageDashboards")!,
            };

            bool isValidRedirect = expectedRedirects.Any(p =>
                refererPath.Equals(p, StringComparison.OrdinalIgnoreCase)
                || refererPath.StartsWith(p + "?", StringComparison.OrdinalIgnoreCase)
            );

            ViewBag.PrevUrl = isValidRedirect ? refererPath : defaultUrl;

            // Now we have both direct announcements and global announcements from the DashboardAnnouncementMap
            var allAnnouncements = dashboard
                .Announcements.Union(dashboard.DashboardAnnouncements.Select(da => da.Announcement))
                .OrderByDescending(a => a.CreatedAt) // Order all announcements together
                .ToList();

            // Map to VM with fallback default of 50
            var viewModel = new DashboardDetailsViewModel
            {
                Id = dashboard.Id,
                Name = dashboard.Name,
                Description = dashboard.Description,
                AuthorName = dashboard.Author.Name,
                AuthorId = dashboard.AuthorId,
                CurrentUserId = userId,
                ProfileImageBase64 = dashboard.Author.ProfileImageBase64 ?? string.Empty,
                Announcements = allAnnouncements
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
                        HasPoll = a.IsPoll,
                    })
                    .ToList(),
                ExpiryOption = (int)dashboard.ExpiryOption,
                MaxAnnouncements = dashboard.MaxAnnouncements,
            };

            return View("Details", viewModel);
        }

        [UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
        [HttpPost("htmx/{dashboardId}/generate-pin")]
        public async Task<IActionResult> GeneratePreviewPin(Guid dashboardId)
        {
            Guid currentUserId = User.GetRequiredId();

            if (!User.HasManageDashboardAccess(dashboardId, _db))
            {
                _logger.LogWarning(
                    "Manager {UserId} is not authorized to generate a PIN for dashboard {DashboardId}",
                    currentUserId,
                    dashboardId
                );
                return Forbid();
            }

            // Remove expired pins
            _db.DashboardPreviewPins.RemoveRange(_db.DashboardPreviewPins.Where(p => p.Expiration <= DateTime.UtcNow));
            await _db.SaveChangesAsync();

            string? uniquePin = await GenerateUniquePinAsync(maxAttempts: MAX_ATTEMPTS);

            if (uniquePin == null)
            {
                _logger.LogError("Could not generate a unique PIN after many attempts");
                Response.Htmx(h =>
                    h.WithTrigger("sendToast", new { msg = "Could not generate PIN. Try again later.", bg = "danger" })
                );
                return StatusCode(500, "PIN generation failed");
            }

            var newPin = new DashboardPreviewPin
            {
                Pin = uniquePin,
                DashboardId = dashboardId,
                Expiration = DateTime.UtcNow.AddSeconds(PIN_EXPIRATION_SECONDS),
            };

            await _db.DashboardPreviewPins.AddAsync(newPin);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "User {UserId} generated a preview PIN for dashboard {DashboardId}: {Pin}",
                currentUserId,
                dashboardId,
                uniquePin
            );

            Response.Htmx(h => h.WithTrigger("sendToast", new { msg = "Pin created", bg = "success" }));

            return PartialView(
                "_PinDisplay",
                new PinDisplayViewModel { Pin = newPin.Pin, ExpiresAt = newPin.Expiration.ToString("u") }
            );
        }

        private async Task<string?> GenerateUniquePinAsync(int maxAttempts)
        {
            var random = new Random();

            for (int i = 0; i < maxAttempts; i++)
            {
                string candidate = random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
                bool exists = await _db.DashboardPreviewPins.AnyAsync(p => p.Pin == candidate);

                if (!exists)
                {
                    return candidate;
                }
            }

            return null;
        }

        private List<SelectListItem> GetDashboardSelectList(Guid userId, Guid? selectedDashboardId = null)
        {
            return _db
                .UserDashboards.Include(ud => ud.Dashboard)
                .Where(ud => ud.UserId == userId)
                .Select(ud => ud.Dashboard)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name,
                    Selected = selectedDashboardId.HasValue && d.Id == selectedDashboardId.Value,
                })
                .ToList();
        }

        private static List<SelectListItem> GetCategorySelectList(AnnouncementCategory? selectedCategory = null)
        {
            return Enum.GetValues<AnnouncementCategory>()
                .Cast<AnnouncementCategory>()
                .Select(c => new SelectListItem
                {
                    Value = ((int)c).ToString(CultureInfo.InvariantCulture),
                    Text = c.ToString(),
                    Selected = selectedCategory.HasValue && c == selectedCategory.Value,
                })
                .ToList();
        }
    }
}
