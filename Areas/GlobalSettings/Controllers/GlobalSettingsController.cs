using Application.Api.Attributes;
using Application.Api.Extensions;
using Application.Areas.GlobalSettings.Models;
using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DbModels = Application.Infrastructure.Database.Models;

namespace Application.Areas.GlobalSettings.Controllers;

[Area("GlobalSettings")]
[Route("settings")]
[UserExists(Roles = new[] { UserRole.Admin })]
public class GlobalSettingsController : Controller
{
    private readonly DatabaseContext _db;
    private readonly ILogger<GlobalSettingsController> _logger;

    public GlobalSettingsController(DatabaseContext db, ILogger<GlobalSettingsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        DbModels.GlobalSettings settings = await GetOrCreateGlobalSettingsAsync();
        var model = new GlobalSettingsViewModel
        {
            AllowedDomainList = settings.AllowedEmailDomains.Select(d => d.Domain).ToList() ?? new List<string>(),
        };

        return View(model);
    }

    [HttpGet("email-domains")]
    public async Task<IActionResult> GetAllowedDomainsList()
    {
        DbModels.GlobalSettings settings = await GetOrCreateGlobalSettingsAsync();
        List<string> domains = settings.AllowedEmailDomains.Select(d => d.Domain).ToList();
        return PartialView("_AllowedDomains", domains);
    }

    [HttpPost("htmx/email-domain")]
    public async Task<IActionResult> AddDomain(GlobalSettingsViewModel model)
    {
        DbModels.GlobalSettings settings = await GetOrCreateGlobalSettingsAsync();

        if (!ModelState.IsValid)
        {
            model.AllowedDomainList = settings.AllowedEmailDomains.Select(d => d.Domain).ToList();
            return PartialView("_AllowedDomainsManagement", model);
        }

        string domainToAdd = model.NewDomain.Trim().ToLowerInvariant();
        var newAllowedDomain = new AllowedDomain { Domain = domainToAdd, GlobalSettingsId = settings.Id };

        _db.AllowedDomains.Add(newAllowedDomain);

        try
        {
            await _db.SaveChangesAsync();

            model.AllowedDomainList = settings.AllowedEmailDomains.Select(d => d.Domain).ToList();
            model.NewDomain = string.Empty;

            _logger.LogInformation(
                "User {UserId} added an allowed email domain: {Domain}",
                User.GetRequiredId(),
                newAllowedDomain
            );

            Response.Htmx(h =>
            {
                h.WithTrigger("sendToast", new { msg = "Domain added.", bg = "success" });
            });

            return PartialView("_AllowedDomainsManagement", model);
        }
        // Raised when a unique constraint is violated
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "User {UserId} tried to add an already added allowed email domain: {Domain}",
                User.GetRequiredId(),
                newAllowedDomain
            );
            ModelState.AddModelError("", "This domain is already registered.");
            model.AllowedDomainList = settings.AllowedEmailDomains.Select(d => d.Domain).ToList();
            return PartialView("_AllowedDomainsManagement", model);
        }
    }

    [HttpDelete("htmx/email-domain/{domain}")]
    public async Task<IActionResult> DeleteDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return BadRequest("Domain cannot be empty.");
        }

        DbModels.GlobalSettings settings = await GetOrCreateGlobalSettingsAsync();
        AllowedDomain? domainEntity = settings.AllowedEmailDomains.FirstOrDefault(d =>
            d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)
        );

        if (domainEntity != null)
        {
            _db.AllowedDomains.Remove(domainEntity);
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "User {UserId} removed an allowed email domain: {Domain}",
                User.GetRequiredId(),
                domain
            );
            Response.Htmx(h =>
            {
                h.WithTrigger("sendToast", new { msg = "Domain removed.", bg = "warning" });
                h.WithTrigger("domain-list-updated");
            });
        }
        else
        {
            _logger.LogWarning(
                "User {UserId} tried to delete an unknown allowed email domain: {Domain}",
                User.GetRequiredId(),
                domain
            );
            Response.Htmx(h =>
            {
                h.WithTrigger("sendToast", new { msg = "Domain not found.", bg = "danger" });
            });
        }

        return Ok();
    }

    /// <summary>
    /// Retrieves or creates the global settings record in the database.
    /// </summary>
    /// <returns>The global settings object.</returns>
    private async Task<DbModels.GlobalSettings> GetOrCreateGlobalSettingsAsync()
    {
        DbModels.GlobalSettings? settings = await _db
            .GlobalSettings.Include(g => g.AllowedEmailDomains)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            _logger.LogDebug("Creating a new instance of global settings");
            settings = new DbModels.GlobalSettings();
            _db.GlobalSettings.Add(settings);
        }
        return settings;
    }
}
