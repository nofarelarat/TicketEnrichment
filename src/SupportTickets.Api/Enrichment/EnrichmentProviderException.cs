namespace SupportTickets.Api.Enrichment;

/// <summary>The AI provider could not serve the request (outage, rate limit, transport error).</summary>
public class EnrichmentProviderException : Exception
{
    public EnrichmentProviderException(string message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the provider asked us to wait, when it said (for example on HTTP 429).</summary>
    public TimeSpan? RetryAfter { get; }
}
