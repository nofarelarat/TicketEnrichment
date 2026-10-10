namespace SupportTickets.Api.Enrichment;

/// <summary>The AI provider answered, but not with output that matches the enrichment schema.</summary>
public class InvalidEnrichmentOutputException : Exception
{
    public InvalidEnrichmentOutputException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
