namespace SupportTickets.Api.Enrichment;

public enum EnrichmentProviderKind
{
    /// <summary>The built-in mock; needs no credentials. The default.</summary>
    Mock,

    /// <summary>The Claude API; needs the ANTHROPIC_API_KEY environment variable.</summary>
    Claude
}
