namespace SupportTickets.Api.Tickets;

public enum SubmissionOutcome
{
    /// <summary>The ticket was stored and queued for enrichment.</summary>
    Created,

    /// <summary>A ticket with the same id and the same content already exists (idempotent replay).</summary>
    Duplicate,

    /// <summary>A ticket with the same id but different content already exists.</summary>
    Conflict
}
