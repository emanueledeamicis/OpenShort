using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class ClickTrackingBackgroundService : BackgroundService
{
    private const string ServiceStartingMessage = "ClickTrackingBackgroundService is starting.";
    private const string ClickProcessingErrorMessage = "Error occurred while processing click event for {Domain}/{Slug}";
    private const string ServiceCancelledMessage = "ClickTrackingBackgroundService processing was cancelled.";
    private const string ServiceCriticalErrorMessage = "A critical error occurred in ClickTrackingBackgroundService.";
    private const string ServiceStoppingMessage = "ClickTrackingBackgroundService is stopping.";

    private readonly ChannelReader<ClickEvent> _channelReader;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClickTrackingBackgroundService> _logger;

    public ClickTrackingBackgroundService(
        ChannelReader<ClickEvent> channelReader,
        IServiceProvider serviceProvider,
        ILogger<ClickTrackingBackgroundService> logger)
    {
        _channelReader = channelReader;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(ServiceStartingMessage);

        try
        {
            await foreach (var clickEvent in _channelReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Resolve scoped DbContext
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var link = await dbContext.Links
                        .FirstOrDefaultAsync(l => l.Slug == clickEvent.Slug && l.Domain == clickEvent.Domain, stoppingToken);

                    if (link != null)
                    {
                        link.ClickCount++;
                        link.LastAccessedAt = clickEvent.Timestamp;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ClickProcessingErrorMessage, clickEvent.Domain, clickEvent.Slug);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(ServiceCancelledMessage);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, ServiceCriticalErrorMessage);
        }

        _logger.LogInformation(ServiceStoppingMessage);
    }
}
