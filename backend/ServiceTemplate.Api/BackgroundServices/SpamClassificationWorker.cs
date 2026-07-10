using ServiceTemplate.Ports.Input;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Api.BackgroundServices;

/// <summary>
/// Drives ISpamClassifier per CONTRACT.md's AnalyzePageView/AnalyzeDestinationClick: polls
/// IEventRepository for unclassified events and classifies each one — spam analysis never runs
/// synchronously on the tracking request path (see ITrafficTracker).
/// </summary>
public class SpamClassificationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SpamClassificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(configuration.GetValue("SpamClassifier:PollIntervalSeconds", 15));
        var batchSize = configuration.GetValue("SpamClassifier:BatchSize", 50);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ClassifyPendingEventsAsync(batchSize, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SpamClassificationWorker - Error while polling for unclassified events");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task ClassifyPendingEventsAsync(int batchSize, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var spamClassifier = scope.ServiceProvider.GetRequiredService<ISpamClassifier>();

        var pageViews = await eventRepository.ListUnclassifiedAsync(EventType.PageView, batchSize);
        foreach (var pageView in pageViews)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await spamClassifier.AnalyzePageViewAsync(pageView.Id.ToString());
        }

        var clicks = await eventRepository.ListUnclassifiedAsync(EventType.DestinationClick, batchSize);
        foreach (var click in clicks)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await spamClassifier.AnalyzeDestinationClickAsync(click.Id.ToString());
        }
    }
}
