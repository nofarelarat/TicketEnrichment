namespace SupportTickets.Api.Enrichment;

/// <summary>
/// The seam to the external AI model. Implementations return the model's raw text and do no
/// validation; <see cref="EnrichmentResultParser"/> owns that, so every provider is held to
/// the same schema.
/// </summary>
public interface IEnrichmentProvider
{
    /// <exception cref="EnrichmentProviderException">The provider is unavailable or rate limiting.</exception>
    /// <exception cref="OperationCanceledException">The attempt timed out or the host is stopping.</exception>
    Task<string> RequestEnrichmentAsync(string subject, string message, CancellationToken cancellationToken);
}
