using PurchaseTracker.Worker.Services;

namespace PurchaseTracker.Worker;

public class ImapWorker : BackgroundService
{
    private readonly ILogger<ImapWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImapWorker(
        ILogger<ImapWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImapWorker starting...");

        var pollSeconds = _configuration.GetValue<int>("Worker:PollSeconds", 30);
        var delay = TimeSpan.FromSeconds(pollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var imapService = scope.ServiceProvider.GetRequiredService<ImapService>();

                _logger.LogInformation("ImapWorker: polling inbox at {Time}", DateTimeOffset.UtcNow);
                var result = await imapService.ProcessInboxAsync(stoppingToken);
                _logger.LogInformation("ImapWorker: processed {Count} messages, {Saved} saved, {Skipped} skipped",
                    result.ProcessedCount, result.SavedCount, result.SkippedCount);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImapWorker: error during inbox processing");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ImapWorker stopping");
    }
}
