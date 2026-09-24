using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Infrastructure.Services;

public class NotificationRuleEngineBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationRuleEngineBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public NotificationRuleEngineBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationRuleEngineBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationRuleEngineBackgroundService started.");

        // Initial delay so the app starts smoothly
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<IAdminNotificationService>();

                _logger.LogInformation("Evaluating automated notification rules...");
                var generatedCount = await notificationService.TriggerAutomatedRulesAsync(stoppingToken);
                _logger.LogInformation("Automated notification rules evaluated. Notifications dispatched: {Count}", generatedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing notification rules engine.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("NotificationRuleEngineBackgroundService stopped.");
    }
}
