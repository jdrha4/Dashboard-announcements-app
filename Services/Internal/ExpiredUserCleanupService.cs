using Application.Services.Internal;

namespace Application.Services
{
    /// <summary>
    /// Periodically deletes expired email-confirmation tokens.
    /// </summary>
    public class ExpiredUserCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ExpiredUserCleanupService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        public ExpiredUserCleanupService(IServiceProvider services, ILogger<ExpiredUserCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Started ExpiredUserCleanupService, interval {Interval}", Interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var cleanupSvc = scope.ServiceProvider.GetRequiredService<EmailConfirmationService>();

                    await cleanupSvc.CleanupExpiredTokensAndUsersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during expired-user cleanup");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("Stopping ExpiredUserCleanupService");
        }
    }
}
