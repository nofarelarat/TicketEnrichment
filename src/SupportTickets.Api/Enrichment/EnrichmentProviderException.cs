namespace SupportTickets.Api.Enrichment;

/// <summary>The AI provider could not serve the request (outage, rate limit, transport error).</summary>
public class EnrichmentProviderException : Exception
{
    public EnrichmentProviderException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
