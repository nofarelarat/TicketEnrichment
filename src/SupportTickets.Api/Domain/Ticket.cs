namespace SupportTickets.Api.Domain;

/// <summary>
/// A support ticket and its enrichment state. The row doubles as the work-queue entry:
/// a ticket is due for enrichment while it is <see cref="EnrichmentStatus.Pending"/> and
/// <see cref="NextAttemptAt"/> has passed.
/// </summary>
public class Ticket
{
    public const int MaxLastErrorLength = 500;

    public Ticket(string id, string subject, string message, DateTimeOffset createdAt)
    {
        Id = id;
        Subject = subject;
        Message = message;
        CreatedAt = createdAt;
        EnrichmentStatus = EnrichmentStatus.Pending;
        NextAttemptAt = createdAt;
    }

    public string Id { get; private set; }

    public string Subject { get; private set; }

    public string Message { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public EnrichmentStatus EnrichmentStatus { get; private set; }

    public TicketCategory? Category { get; private set; }

    public TicketPriority? Priority { get; private set; }

    public string? Summary { get; private set; }

    public DateTimeOffset? EnrichedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>Diagnostic only; never returned to API clients.</summary>
    public string? LastError { get; private set; }

    public bool HasSameContent(string subject, string message) =>
        string.Equals(Subject, subject, StringComparison.Ordinal) &&
        string.Equals(Message, message, StringComparison.Ordinal);

    public void CompleteEnrichment(TicketEnrichment enrichment, DateTimeOffset now)
    {
        EnsurePending();

        AttemptCount++;
        Category = enrichment.Category;
        Priority = enrichment.Priority;
        Summary = enrichment.Summary;
        EnrichedAt = now;
        EnrichmentStatus = EnrichmentStatus.Completed;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <param name="error">Why the attempt failed.</param>
    /// <param name="retryAt">When to try again, or <c>null</c> to give up and mark the ticket failed.</param>
    public void RecordFailedAttempt(string error, DateTimeOffset? retryAt)
    {
        EnsurePending();

        AttemptCount++;
        LastError = error.Length <= MaxLastErrorLength ? error : error[..MaxLastErrorLength];
        NextAttemptAt = retryAt;

        if (retryAt is null)
        {
            EnrichmentStatus = EnrichmentStatus.Failed;
        }
    }

    private void EnsurePending()
    {
        if (EnrichmentStatus != EnrichmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Ticket '{Id}' is {EnrichmentStatus}; only pending tickets can record enrichment attempts.");
        }
    }
}
