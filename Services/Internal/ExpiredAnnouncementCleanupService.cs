using Application.Infrastructure.Database;
using Application.Infrastructure.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Internal
{
    public class ExpiredAnnouncementCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ExpiredAnnouncementCleanupService> _logger;

        // Run each night at 01:00 UTC -> 3 AM in Czechia
        private const int CleanupHourUtc = 1;

        public ExpiredAnnouncementCleanupService(
            IServiceProvider services,
            ILogger<ExpiredAnnouncementCleanupService> logger
        )
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExpiredAnnouncementCleanupService starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddHours(CleanupHourUtc);
                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("Next cleanup scheduled in {Delay}.", delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    await DeleteExpiredAnnouncementsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during expired-announcement cleanup");
                }
            }

            _logger.LogInformation("ExpiredAnnouncementCleanupService stopping.");

            await CleanupAllDashboardsNonImportantAnnouncementsAsync(stoppingToken);

            _logger.LogInformation(
                "Triggered nonimportant announcements cleanup. That exceed max announcement number."
            );
        }

        private async Task DeleteExpiredAnnouncementsAsync(CancellationToken ct)
        {
            using IServiceScope scope = _services.CreateScope();
            DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            DateTime now = DateTime.UtcNow;
            List<Announcement> expired = await db
                .Announcements.Where(a => !a.IsImportant && a.ExpirationDate < now)
                .ToListAsync(ct);

            if (expired.Count == 0)
            {
                _logger.LogInformation("No expired announcements found at {Time}.", now);
                return;
            }

            db.Announcements.RemoveRange(expired);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted {Count} expired announcements at {Time}.", expired.Count, now);
        }

        private async Task CleanupAllDashboardsNonImportantAnnouncementsAsync(CancellationToken ct)
        {
            using IServiceScope scope = _services.CreateScope();
            DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var dashboards = await db
                .Dashboards.Select(d => new
                {
                    d.Id,
                    MaxAnnouncements = d.MaxAnnouncements > 0 ? d.MaxAnnouncements : 50,
                })
                .ToListAsync(ct);

            foreach (var dashboard in dashboards)
            {
                var nonImportant = await db
                    .Announcements.Where(a => a.DashboardId == dashboard.Id && !a.IsImportant)
                    .OrderBy(a => a.CreatedAt)
                    .ToListAsync(ct);

                int currentCount = nonImportant.Count;
                int allowedMax = dashboard.MaxAnnouncements;

                if (currentCount > allowedMax)
                {
                    int excess = currentCount - allowedMax;
                    List<Announcement> toDelete = nonImportant.Take(excess).ToList();

                    db.Announcements.RemoveRange(toDelete);

                    _logger.LogInformation(
                        "Deleted {DeletedCount} oldest non-important announcements for dashboard {DashboardId} to maintain the max limit of {Max}",
                        toDelete.Count,
                        dashboard.Id,
                        allowedMax
                    );
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
