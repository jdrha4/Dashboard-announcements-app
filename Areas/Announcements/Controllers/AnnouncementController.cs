using System.Globalization;
using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Api.Utils;
using Application.Areas.Announcements.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Htmx;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Application.Areas.Announcements.Controllers;

[Area("Announcements")]
[Route("announcement")]
[UserExists]
public class AnnouncementController : Controller
{
    private readonly DatabaseContext _db;
    private readonly ILogger<AnnouncementController> _logger;

    public AnnouncementController(DatabaseContext databaseContext, ILogger<AnnouncementController> logger)
    {
        _db = databaseContext;
        _logger = logger;
    }

    [HttpGet("create")]
    public IActionResult Create(Guid dashboardId)
    {
        Guid userId = User.GetRequiredId();

        var dashboard = _db
            .Dashboards.Where(d => d.Id == dashboardId)
            .Select(d => new { d.AuthorId, d.ExpiryOption })
            .FirstOrDefault();

        if (dashboard == null)
        {
            _logger.LogDebug("User tried to create an announcemnt on a dashboard that doesn't exist.");
            return NotFound("Did not find the dashboard.");
        }

        if (!User.HasDashboardAccess(dashboardId, _db))
        {
            _logger.LogDebug(
                "User {UserId} attempted to visit the create announcement page of dashboard {DashboardId}, which he doesn't have access to",
                userId,
                dashboardId
            );
            return Forbid();
        }

        DateTime maxAllowed = dashboard.ExpiryOption switch
        {
            ExpiryOption.OneWeek => DateTime.UtcNow.Date.AddDays(2),
            ExpiryOption.TwoWeeks => DateTime.UtcNow.Date.AddDays(14),
            ExpiryOption.OneMonth => DateTime.UtcNow.Date.AddMonths(1),
            ExpiryOption.TwoMonths => DateTime.UtcNow.Date.AddMonths(2),
            ExpiryOption.SixMonths => DateTime.UtcNow.Date.AddMonths(6),
            ExpiryOption.TwelveMonths => DateTime.UtcNow.Date.AddMonths(12),
            _ => DateTime.UtcNow.Date.AddMonths(1),
        };

        var model = new CreateAnnouncementViewModel
        {
            IsImportant = User.HasManageDashboardAccess(dashboardId, _db),
            DashboardAuthorId = dashboard.AuthorId,
            CurrentUserId = User.GetRequiredId(),
            MaxAllowedExpirationDate = maxAllowed,
            ExpirationDate = DateTime.UtcNow.Date,
        };

        ViewBag.Categories = GetCategorySelectList();
        ViewBag.DashboardId = dashboardId;

        // Capture the referer header, to allow redirecting user to the previous page
        // If it's not available, or can't be trusted, show the user dashboard list
        string? redirect = Request.Headers.Referer.ToString();
        Uri? parsedUri = Uri.TryCreate(redirect, UriKind.Absolute, out var uri) ? uri : null;
        if (
            redirect.IsNullOrEmpty()
            || redirect == Request.GetEncodedUrl()
            || !string.Equals(parsedUri?.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
        )
            redirect = Url.Action("Index", "UserDashboards", new { area = "Dashboards", dashboardId });
        else
            redirect = parsedUri!.PathAndQuery;
        ViewBag.PrevUrl = redirect;

        return View("CreateAnnouncement", model);
    }

    [UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
    [HttpGet("create-global")]
    public IActionResult CreateGlobal()
    {
        Guid userId = User.GetRequiredId();

        var dashboards = _db
            .Dashboards.Select(d => new CreateGlobalAnnouncementViewModel.DashboardSelection
            {
                DashboardId = d.Id,
                Name = d.Name,
            })
            .ToList();

        var accessibleDashboards = dashboards.Where(d => User.HasManageDashboardAccess(d.DashboardId, _db)).ToList();

        var model = new CreateGlobalAnnouncementViewModel
        {
            Dashboards = dashboards,
            IsImportant = User.IsInRole("Admin") || User.IsInRole("Manager"),
            CurrentUserId = User.GetRequiredId(),
            DashboardAuthorId = Guid.Empty, // no owner for global announcements
        };

        ViewBag.Categories = GetCategorySelectList();
        ViewBag.PrevUrl =
            Request.Headers.Referer.ToString() ?? Url.Action("Index", "ManageDashboards", new { area = "Dashboards" });

        return View("CreateGlobalAnnouncement", model);
    }

    [UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
    [HttpPost("htmx/create-global")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitGlobalAnnouncement(CreateGlobalAnnouncementViewModel model)
    {
        if (model.SelectedDashboardIds == null || model.SelectedDashboardIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedDashboardIds), "You must select at least one dashboard.");
            ViewBag.Categories = GetCategorySelectList();
            return PartialView("_CreateGlobalAnnouncementForm", model);
        }

        Guid userId = User.GetRequiredId();

        //dashboard access check
        foreach (Guid dashboardId in model.SelectedDashboardIds)
        {
            if (!User.HasManageDashboardAccess(dashboardId, _db))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedDashboardIds),
                    $"You do not have manage access for the dashboard with ID {dashboardId}."
                );
                ViewBag.Categories = GetCategorySelectList();
                return PartialView("_CreateGlobalAnnouncementForm", model);
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = GetCategorySelectList();
            return PartialView("_CreateGlobalAnnouncementForm", model);
        }

        var announcement = new Announcement
        {
            Title = model.Title,
            Description = model.Description,
            Category = model.Category!.Value,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsImportant = model.IsImportant,
            ExpirationDate = model.ExpirationDate,
        };

        await _db.Announcements.AddAsync(announcement);
        await _db.SaveChangesAsync();

        // Collect all the mappings in a list
        List<DashboardAnnouncementMap> mappings = model
            .SelectedDashboardIds.Select(dashboardId => new DashboardAnnouncementMap
            {
                DashboardId = dashboardId,
                AnnouncementId = announcement.Id,
            })
            .ToList();

        // Add all the mappings in a single database call
        await _db.DashboardAnnouncements.AddRangeAsync(mappings);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Global announcement created successfully!";

        string redirectUrl = Url.Action("Index", "ManageDashboards", new { area = "Dashboards" })!;
        Response.Htmx(h => h.Redirect(redirectUrl));
        return Ok();
    }

    [HttpPost("htmx/create")]
    public async Task<IActionResult> CreateAnnouncement(
        CreateAnnouncementViewModel model,
        Guid dashboardId,
        string prevUrl
    )
    {
        if (model.HasPoll)
        {
            if (model.PollChoices.Any(string.IsNullOrWhiteSpace))
            {
                ModelState.AddModelError(nameof(model.PollChoices), "");

                _logger.LogInformation("User tried to create an empty poll choice.");

                Response.Htmx(h =>
                    h.WithTrigger("sendToast", new { msg = $"Poll choices cannot be empty.", bg = "warning" })
                );

                return PartialView("_CreateAnnouncementForm", model);
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = GetCategorySelectList();
            ViewBag.DashboardId = dashboardId;
            ViewBag.PrevUrl = prevUrl;
            return PartialView("_CreateAnnouncementForm", model);
        }

        // Fetch expiry and max config in one query
        var dashboardConfig = await _db
            .Dashboards.Where(d => d.Id == dashboardId)
            .Select(d => new
            {
                ExpiryDays = (int)d.ExpiryOption,
                MaxAnnouncements = d.MaxAnnouncements > 0 ? d.MaxAnnouncements : 50,
            })
            .FirstOrDefaultAsync();

        if (dashboardConfig == null)
            return NotFound("Dashboard not found.");

        Guid userId = User.GetRequiredId();
        bool canManageDashboard = User.HasManageDashboardAccess(dashboardId, _db);

        DateTime maxAllowedFromDashboard = dashboardConfig.ExpiryDays switch
        {
            7 => DateTime.UtcNow.Date.AddDays(7),
            14 => DateTime.UtcNow.Date.AddDays(14),
            30 => DateTime.UtcNow.Date.AddMonths(1),
            60 => DateTime.UtcNow.Date.AddMonths(2),
            180 => DateTime.UtcNow.Date.AddMonths(6),
            365 => DateTime.UtcNow.Date.AddMonths(12),
            _ => DateTime.UtcNow.Date.AddMonths(1),
        };

        if (model.ExpirationDate > maxAllowedFromDashboard && !canManageDashboard)
        {
            Response.Htmx(h =>
                h.WithTrigger(
                    "sendToast",
                    new { msg = $"Expiration cannot exceed {maxAllowedFromDashboard:yyyy-MM-dd}", bg = "danger" }
                )
            );
            ViewBag.Categories = GetCategorySelectList();
            ViewBag.DashboardId = dashboardId;
            ViewBag.PrevUrl = prevUrl;
            return PartialView("_CreateAnnouncementForm", model);
        }

        if (!User.HasDashboardAccess(dashboardId, _db))
        {
            _logger.LogWarning(
                "User {UserId} attempted to create an announcement on dashboard {DashboardId}, which he doesn't have access to",
                userId,
                dashboardId
            );
            return Forbid();
        }

        var announcement = new Announcement
        {
            Title = model.Title,
            Description = model.Description,
            Category = model.Category!.Value,
            UserId = userId,
            DashboardId = dashboardId,
            CreatedAt = DateTime.UtcNow,
            IsImportant = canManageDashboard && model.IsImportant,
            ExpirationDate = model.ExpirationDate,
            IsPoll = model.HasPoll,
        };

        await _db.Announcements.AddAsync(announcement);
        await _db.SaveChangesAsync();

        if (model.HasPoll && User.HasManageDashboardAccess(dashboardId, _db))
        {
            var poll = new Poll { AnnouncementId = announcement.Id, IsMultichoice = model.IsMultichoice };
            poll.Announcement = announcement;

            await _db.Polls.AddAsync(poll);
            await _db.SaveChangesAsync();

            List<PollChoice> pollChoices = model
                .PollChoices.Where(choice => !string.IsNullOrWhiteSpace(choice))
                .Select(choiceText => new PollChoice
                {
                    ChoiceText = choiceText.Trim(),
                    PollId = poll.AnnouncementId,
                    Poll = poll,
                })
                .ToList();

            await _db.PollChoices.AddRangeAsync(pollChoices);
            await _db.SaveChangesAsync();

            announcement.Poll = poll;
            await _db.SaveChangesAsync();
        }

        // 50 item capacity (excluding important)
        List<Announcement> nonImportant = _db
            .Announcements.Where(a => a.DashboardId == dashboardId && !a.IsImportant)
            .OrderBy(a => a.CreatedAt)
            .ToList();

        if (nonImportant.Count > 50)
        {
            int excess = nonImportant.Count - 50;
            IEnumerable<Announcement> toDelete = nonImportant.Take(excess);
            _db.Announcements.RemoveRange(toDelete);
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "User {UserId} created a new announcement ({AnnouncementId}) on dashboard {DashboardId}",
            userId,
            announcement.Id,
            dashboardId
        );

        int currentCount = nonImportant.Count;
        int allowedMax = dashboardConfig.MaxAnnouncements > 0 ? dashboardConfig.MaxAnnouncements : 50;

        if (currentCount > allowedMax)
        {
            int excess = currentCount - allowedMax;
            List<Announcement> toDelete = nonImportant.Take(excess).ToList();

            _db.Announcements.RemoveRange(toDelete);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted {DeletedCount} oldest non-important announcements to maintain the max limit of {Max}",
                toDelete.Count,
                allowedMax
            );
        }

        TempData["SuccessMessage"] = "Announcement created successfully!";
        Response.Htmx(h => h.Redirect(prevUrl));
        return Ok();
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

    [HttpGet("htmx/{announcementId}/details")]
    public async Task<IActionResult> Details(Guid announcementId, string bgColor)
    {
        Announcement? announcement = await _db
            .Announcements.Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement is null)
            return NotFound();

        // Directly resolve the dashboard if it exists
        Dashboard? dashboard = null;

        if (announcement.DashboardId != null)
        {
            dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == announcement.DashboardId);
        }
        else
        {
            // Try resolve from the mapping table (for global announcements)
            dashboard = await _db
                .DashboardAnnouncements.Where(m => m.AnnouncementId == announcement.Id)
                .Select(m => m.Dashboard)
                .FirstOrDefaultAsync();
        }

        if (dashboard == null)
            return NotFound();

        List<CommentViewModel> comments = await _db
            .Comments.Include(c => c.User)
            .Where(c => c.AnnouncementId == announcementId)
            .OrderByDescending(c => c.PostedAt)
            .Select(c => new CommentViewModel
            {
                Author = c.User.Name,
                Content = c.Content,
                PostedAt = c.PostedAt,
            })
            .ToListAsync();

        List<PollChoiceResultViewModel> pollChoices = new();
        bool userHasVoted = false;
        bool isMultichoice = false;

        if (announcement.IsPoll)
        {
            var poll = await _db
                .Polls.Include(p => p.PollChoices)
                .ThenInclude(pc => pc.PollVotes)
                .FirstOrDefaultAsync(p => p.AnnouncementId == announcementId);

            if (poll != null)
            {
                isMultichoice = poll.IsMultichoice;

                pollChoices = poll
                    .PollChoices.Select(pc => new PollChoiceResultViewModel
                    {
                        Id = pc.Id,
                        ChoiceText = pc.ChoiceText,
                        VoteCount = pc.PollVotes.Count,
                        HasUserVoted = pc.PollVotes.Any(v => v.UserId == User.GetRequiredId()),
                    })
                    .ToList();

                userHasVoted = pollChoices.Any(pcvm => pcvm.HasUserVoted);
            }
        }

        var viewModel = new AnnouncementWithConversationViewModel
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Description = announcement.Description,
            UserName = announcement.User.Name,
            ConversationMessages = comments,
            BackgroundColor = bgColor,
            CreatedAt = announcement.CreatedAt,
            IsImportant = announcement.IsImportant,
            AuthorId = announcement.UserId.ToString(),
            CurrentUserId = User.GetRequiredId().ToString(),
            IsUserDashboardOwner = dashboard.AuthorId == User.GetRequiredId(),
            HasPoll = announcement.IsPoll,
            PollChoices = pollChoices,
            UserHasVoted = userHasVoted,
            IsMultichoice = isMultichoice,
        };

        return PartialView("_Details", viewModel);
    }

    [HttpGet("{announcementId}/edit")]
    public async Task<IActionResult> Edit(Guid announcementId)
    {
        Guid userId = User.GetRequiredId();

        Announcement? announcement = await _db
            .Announcements.Include(a => a.Dashboard)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null)
            return NotFound();

        if (announcement.DashboardId == null || announcement.Dashboard == null)
        {
            return RedirectToAction(nameof(EditGlobal), new { announcementId });
        }

        if (!User.HasAnnouncementAccess(announcement, _db))
            return Forbid();

        EditAnnouncementViewModel model = new EditAnnouncementViewModel
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Description = announcement.Description,
            Category = announcement.Category,
            DashboardId = announcement.Dashboard.Id,
            PrevUrl = Request.Headers.Referer.ToString() ?? "/",
            IsImportant = announcement.IsImportant,
            DashboardAuthorId = announcement.Dashboard.AuthorId,
            CurrentUserId = User.GetRequiredId(),
        };

        return View("Edit", model);
    }

    [HttpPost("htmx/{announcementId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitEdit(Guid announcementId, EditAnnouncementViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_EditForm", model);

        Announcement? announcement = await _db
            .Announcements.Include(a => a.Dashboard)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null)
            return NotFound();

        if (announcement.Dashboard == null)
        {
            DashboardAnnouncementMap? dashboardMap = await _db
                .DashboardAnnouncements.Include(m => m.Dashboard)
                .FirstOrDefaultAsync(m => m.AnnouncementId == announcement.Id);

            if (dashboardMap == null || dashboardMap.Dashboard == null)
                return BadRequest("Announcement is not associated with any dashboard.");

            announcement.Dashboard = dashboardMap.Dashboard;
        }

        if (!User.HasAnnouncementAccess(announcement, _db))
            return Forbid();

        bool canMarkImportant = User.HasManageDashboardAccess(model.DashboardId, _db);

        announcement.Title = model.Title;
        announcement.Description = model.Description;
        announcement.Category = model.Category!.Value;
        announcement.IsImportant = canMarkImportant && model.IsImportant;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Announcement updated.";
        Response.Htmx(h => h.Redirect(model.PrevUrl));
        return Ok();
    }

    [UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
    [HttpGet("{announcementId}/edit-global")]
    public async Task<IActionResult> EditGlobal(Guid announcementId)
    {
        Announcement? announcement = await _db
            .Announcements.Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null)
            return NotFound();

        List<Guid> dashboardIds = await _db
            .DashboardAnnouncements.Where(m => m.AnnouncementId == announcementId)
            .Select(m => m.DashboardId)
            .ToListAsync();

        var dashboards = await _db
            .Dashboards.Select(d => new CreateGlobalAnnouncementViewModel.DashboardSelection
            {
                DashboardId = d.Id,
                Name = d.Name,
            })
            .ToListAsync();

        // Prepare the redirect URL based on the referer
        string? referer = Request.Headers.Referer.ToString();
        string defaultUrl = Url.Action("Index", "ManageDashboards", new { area = "Dashboards" })!;
        _ = Uri.TryCreate(referer, UriKind.Absolute, out Uri? parsedUri);
        bool isSameHost = string.Equals(parsedUri?.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase);
        string prevUrl = isSameHost ? referer : defaultUrl;

        var model = new EditGlobalAnnouncementViewModel
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Description = announcement.Description,
            Category = announcement.Category,
            IsImportant = announcement.IsImportant,
            ExpirationDate = announcement.ExpirationDate,
            SelectedDashboardIds = dashboardIds,
            Dashboards = dashboards,
            CurrentUserId = User.GetRequiredId(),
            DashboardAuthorId = Guid.Empty,
            PrevUrl = prevUrl,
        };

        ViewBag.Categories = GetCategorySelectList();
        return View("EditGlobal", model);
    }

    [UserExists(Roles = new[] { UserRole.Manager, UserRole.Admin })]
    [HttpPost("htmx/{announcementId}/edit-global")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitEditGlobalAnnouncement(
        Guid announcementId,
        EditGlobalAnnouncementViewModel model
    )
    {
        if (model.SelectedDashboardIds == null || model.SelectedDashboardIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedDashboardIds), "You must select at least one dashboard.");
            ViewBag.Categories = GetCategorySelectList(model.Category);
            return PartialView("_EditGlobalAnnouncementForm", model);
        }

        Announcement? announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null)
            return NotFound();

        foreach (Guid dashboardId in model.SelectedDashboardIds)
        {
            if (!User.HasManageDashboardAccess(dashboardId, _db))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedDashboardIds),
                    $"You do not have manage access for dashboard {dashboardId}."
                );
                ViewBag.Categories = GetCategorySelectList(model.Category);
                return PartialView("_EditGlobalAnnouncementForm", model);
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = GetCategorySelectList(model.Category);
            return PartialView("_EditGlobalAnnouncementForm", model);
        }

        // Update announcement
        announcement.Title = model.Title;
        announcement.Description = model.Description;
        announcement.Category = model.Category!.Value;
        announcement.ExpirationDate = model.ExpirationDate;
        announcement.IsImportant = model.IsImportant;

        // Replace dashboard mappings
        List<DashboardAnnouncementMap> existingMappings = await _db
            .DashboardAnnouncements.Where(m => m.AnnouncementId == announcementId)
            .ToListAsync();

        _db.DashboardAnnouncements.RemoveRange(existingMappings);

        await _db.SaveChangesAsync();

        IEnumerable<DashboardAnnouncementMap> newMappings = model.SelectedDashboardIds.Select(
            dashboardId => new DashboardAnnouncementMap { DashboardId = dashboardId, AnnouncementId = announcement.Id }
        );

        await _db.DashboardAnnouncements.AddRangeAsync(newMappings);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Global announcement updated successfully!";

        string defaultUrl = Url.Action("Index", "ManageDashboards", new { area = "Dashboards" })!;
        string redirectUrl = string.IsNullOrEmpty(model.PrevUrl) ? defaultUrl : model.PrevUrl;
        Response.Htmx(h => h.Redirect(redirectUrl));
        return Ok();
    }

    [HttpPost("htmx/{announcementId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid announcementId, [FromForm] string? prevUrl)
    {
        Announcement? announcement = await _db
            .Announcements.Include(a => a.Dashboard)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null)
            return NotFound();

        if (!User.HasAnnouncementAccess(announcement, _db))
            return Forbid();

        IQueryable<DashboardAnnouncementMap> mappings = _db.DashboardAnnouncements.Where(m =>
            m.AnnouncementId == announcement.Id
        );
        _db.DashboardAnnouncements.RemoveRange(mappings);
        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Announcement deleted successfully.";

        string defaultUrl;
        if (announcement.DashboardId != null)
        {
            defaultUrl = Url.Action(
                "Details",
                "UserDashboards",
                new { area = "Dashboards", dashboardId = announcement.DashboardId }
            )!;
        }
        else
        {
            defaultUrl = Url.Action("Index", "ManageDashboards", new { area = "Dashboards" })!;
        }

        string redirectUrl = string.IsNullOrEmpty(prevUrl) ? defaultUrl : prevUrl;
        Response.Htmx(h => h.Redirect(redirectUrl));
        return Ok();
    }

    [HttpPost("htmx/{announcementId}/comment")]
    public async Task<IActionResult> AddComment(
        Guid announcementId,
        [FromForm] string comment,
        [FromForm] string backgroundColor
    )
    {
        Guid userId = User.GetRequiredId();

        Announcement? announcement = await _db
            .Announcements.Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null || announcement.User == null)
        {
            _logger.LogWarning(
                "User {UserId} attempted to add a comment to non-existent announcement ({AnnouncementId})",
                userId,
                announcementId
            );
            return NotFound();
        }

        if (string.IsNullOrEmpty(comment))
        {
            ModelState.AddModelError(nameof(AnnouncementWithConversationViewModel.Comment), "Comment cannot be empty.");
            AnnouncementWithConversationViewModel? model = await BuildConversationModel(announcement, comment);
            return PartialView("_ConversationPartial", model);
        }

        if (WordFilter.ContainsBannedWords(comment, out string bannedWord))
        {
            _logger.LogInformation(
                "User {UserId} tried to send a comment with banned word: {BannedWord}",
                userId,
                bannedWord
            );
            ModelState.AddModelError(
                nameof(AnnouncementWithConversationViewModel.Comment),
                $"Your comment contains a banned word: \"{bannedWord}\"."
            );
            AnnouncementWithConversationViewModel? model = await BuildConversationModel(announcement, comment);
            return PartialView("_ConversationPartial", model);
        }

        Comment newComment = new Comment
        {
            Id = Guid.NewGuid(),
            AnnouncementId = announcementId,
            UserId = userId,
            Content = comment,
            PostedAt = DateTime.UtcNow,
        };

        _db.Comments.Add(newComment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} has added a new comment ({CommentId}) on announcement {AnnouncementId}",
            userId,
            newComment.Id,
            announcement.Id
        );

        // Clear the comment field in the model before passing it back
        AnnouncementWithConversationViewModel updatedModel = await BuildConversationModel(announcement);
        updatedModel.BackgroundColor = backgroundColor;
        updatedModel.Comment = string.Empty;
        ModelState.Remove("Comment");

        return PartialView("_ConversationPartial", updatedModel);
    }

    [HttpPost("htmx/{announcementId}/vote")]
    public async Task<IActionResult> Vote(Guid announcementId, Guid[] pollChoices)
    {
        Guid userId = User.GetRequiredId();

        Announcement? announcement = await _db
            .Announcements.Include(a => a.Poll!)
            .ThenInclude(p => p.PollChoices)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (announcement == null || announcement.Poll == null)
        {
            _logger.LogWarning(
                "User {UserId} attempted to vote on a non-existent announcement {AnnouncementId} or non-existing poll",
                userId,
                announcementId
            );
            return NotFound("Announcement or poll not found.");
        }

        Poll poll = announcement.Poll;

        if (!poll.IsMultichoice && pollChoices.Length > 1)
        {
            _logger.LogWarning(
                "User {UserId} attempted to pass multiple votes on a single choice poll {PollId}",
                userId,
                poll.AnnouncementId
            );
            return BadRequest("Only one choice allowed for single-choice polls.");
        }

        if (pollChoices.Length == 0)
        {
            _logger.LogDebug("User {UserId} did not select any choice on poll {PollId}", userId, poll.AnnouncementId);
            return BadRequest("No choices selected.");
        }

        if (poll.PollChoices.SelectMany(pc => pc.PollVotes).Any(vote => vote.UserId == userId))
        {
            _logger.LogDebug("User {UserId} has already voted on poll {PollId}", userId, poll.AnnouncementId);
            return BadRequest("You have already voted.");
        }

        foreach (var choiceId in pollChoices)
        {
            PollChoice? pollChoice = poll.PollChoices.FirstOrDefault(c => c.Id == choiceId);

            if (pollChoice != null)
            {
                var pollVote = new PollVote
                {
                    UserId = userId,
                    PollChoiceId = pollChoice.Id,
                    PollChoice = pollChoice,
                    User = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found."),
                };

                _db.PollVotes.Add(pollVote);
            }
        }

        await _db.SaveChangesAsync();

        // Now fetch the updated announcement to map to the view model
        var updatedAnnouncement = await _db
            .Announcements.Include(a => a.Poll!)
            .ThenInclude(p => p.PollChoices)
            .ThenInclude(pc => pc.PollVotes)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == announcementId);

        if (updatedAnnouncement == null)
        {
            return NotFound();
        }

        AnnouncementWithConversationViewModel model = await BuildConversationModel(updatedAnnouncement);

        Response.Htmx(h =>
            h.WithTrigger(
                "sendToast",
                new
                {
                    msg = "Successfully voted.",
                    bg = "success",
                    position = "bottom-center",
                }
            )
        );

        return PartialView("_Poll", model);
    }

    private async Task<AnnouncementWithConversationViewModel> BuildConversationModel(
        Announcement announcement,
        string comment = ""
    )
    {
        List<CommentViewModel> comments = await _db
            .Comments.Include(c => c.User)
            .Where(c => c.AnnouncementId == announcement.Id)
            .OrderByDescending(c => c.PostedAt)
            .Select(c => new CommentViewModel
            {
                Author = c.User.Name,
                Content = c.Content,
                PostedAt = c.PostedAt,
            })
            .ToListAsync();

        Guid userId = User.GetRequiredId();

        return new AnnouncementWithConversationViewModel
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Description = announcement.Description,
            UserName = announcement.User.Name,
            ConversationMessages = comments,
            Comment = comment,
            BackgroundColor = "bg-light",
            CreatedAt = announcement.CreatedAt,
            HasPoll = announcement.IsPoll,
            PollChoices =
                announcement
                    .Poll?.PollChoices.Select(pc => new PollChoiceResultViewModel
                    {
                        Id = pc.Id,
                        ChoiceText = pc.ChoiceText,
                        VoteCount = pc.PollVotes.Count,
                        HasUserVoted = pc.PollVotes.Any(v => v.UserId == userId),
                    })
                    .ToList() ?? new List<PollChoiceResultViewModel>(),
        };
    }

    [HttpGet("htmx/{announcementId}/delete-confirm")]
    public IActionResult ConfirmDelete(Guid announcementId, string? prevUrl)
    {
        if (!string.IsNullOrEmpty(prevUrl))
        {
            ViewBag.PrevUrl = prevUrl;
        }
        return PartialView("_ConfirmDelete", announcementId);
    }
}
