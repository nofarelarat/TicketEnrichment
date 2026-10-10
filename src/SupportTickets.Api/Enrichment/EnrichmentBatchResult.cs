namespace SupportTickets.Api.Enrichment;

public class EnrichmentBatchResult
{
    public EnrichmentBatchResult(int attempted, int completed, bool providerUnhealthy, bool moreWorkLikely)
    {
        Attempted = attempted;
        Completed = completed;
        ProviderUnhealthy = providerUnhealthy;
        MoreWorkLikely = moreWorkLikely;
    }

    public int Attempted { get; }

    public int Completed { get; }

    /// <summary>The batch stopped early because the provider failed, timed out or rate limited.</summary>
    public bool ProviderUnhealthy { get; }

    /// <summary>The batch was full and the provider is healthy, so the worker should not sleep.</summary>
    public bool MoreWorkLikely { get; }
}
