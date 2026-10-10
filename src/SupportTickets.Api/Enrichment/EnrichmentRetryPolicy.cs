using Microsoft.Extensions.Options;

namespace SupportTickets.Api.Enrichment;

/// <summary>Decides whether a failed enrichment attempt is retried, and after how long.</summary>
public class EnrichmentRetryPolicy
{
    private readonly EnrichmentOptions _options;

    public EnrichmentRetryPolicy(IOptions<EnrichmentOptions> options)
    {
        _options = options.Value;
    }

    /// <param name="attemptsMade">Attempts made so far, including the one that just failed.</param>
    /// <param name="retryAfter">A wait the provider asked for, if any.</param>
    /// <returns>The delay before the next attempt, or <c>null</c> when no attempts are left.</returns>
    public TimeSpan? GetRetryDelay(int attemptsMade, TimeSpan? retryAfter)
    {
        if (attemptsMade >= _options.MaxAttempts)
        {
            return null;
        }

        var exponent = Math.Max(0, attemptsMade - 1);
        var backoffMs = _options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);
        backoffMs = Math.Min(backoffMs, _options.MaxRetryDelay.TotalMilliseconds);

        // Jitter keeps tickets that failed together from retrying together.
        var jitter = 1 + ((Random.Shared.NextDouble() * 2) - 1) * _options.JitterFactor;
        var delay = TimeSpan.FromMilliseconds(backoffMs * jitter);

        // The provider's own Retry-After wins when it is longer, even past MaxRetryDelay.
        if (retryAfter is not null && retryAfter.Value > delay)
        {
            delay = retryAfter.Value;
        }

        return delay;
    }
}
