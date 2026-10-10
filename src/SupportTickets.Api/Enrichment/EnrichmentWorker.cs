using Microsoft.Extensions.Options;

namespace SupportTickets.Api.Enrichment;

/// <summary>Hosts the enrichment loop: process what is due, then sleep until signalled or the poll interval.</summary>
public class EnrichmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnrichmentSignal _signal;
    private readonly EnrichmentOptions _options;
    private readonly ILogger<EnrichmentWorker> _logger;

    public EnrichmentWorker(
        IServiceScopeFactory scopeFactory,
        EnrichmentSignal signal,
        IOptions<EnrichmentOptions> options,
        ILogger<EnrichmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled)
        {
            _logger.LogInformation("Enrichment worker is disabled.");
            return;
        }

        // Yield so host startup is not blocked by the first pass.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var moreWorkLikely = false;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EnrichmentProcessor>();
                var result = await processor.ProcessDueAsync(stoppingToken);
                moreWorkLikely = result.MoreWorkLikely;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad pass (for example a transient database error) stop the worker.
                _logger.LogError(ex, "Enrichment pass failed; will retry on the next poll.");
            }

            if (moreWorkLikely)
            {
                continue;
            }

            try
            {
                await _signal.WaitAsync(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
