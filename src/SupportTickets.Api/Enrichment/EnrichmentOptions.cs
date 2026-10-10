using System.ComponentModel.DataAnnotations;

namespace SupportTickets.Api.Enrichment;

public class EnrichmentOptions
{
    public const string SectionName = "Enrichment";

    /// <summary>Which AI provider enriches tickets.</summary>
    public EnrichmentProviderKind Provider { get; set; } = EnrichmentProviderKind.Mock;

    /// <summary>Set to false to run the API without the background worker (used by tests).</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>How long the worker sleeps between passes; the longest a new ticket waits to be picked up.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 20;

    /// <summary>How long one provider call may take before it counts as a failed attempt.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Total attempts per ticket before it is marked Failed.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay before the second attempt; doubles for each attempt after that.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Random spread applied to each delay, as a fraction of it (0.2 means plus or minus 20%).</summary>
    [Range(0.0, 1.0)]
    public double JitterFactor { get; set; } = 0.2;
}
