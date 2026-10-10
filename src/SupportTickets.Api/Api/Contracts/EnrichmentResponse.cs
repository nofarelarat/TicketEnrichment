using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Api.Contracts;

public class EnrichmentResponse
{
    public EnrichmentStatus Status { get; set; }

    /// <summary>Null until the status is Completed.</summary>
    public TicketCategory? Category { get; set; }

    /// <summary>Null until the status is Completed.</summary>
    public TicketPriority? Priority { get; set; }

    /// <summary>Null until the status is Completed.</summary>
    public string? Summary { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? EnrichedAt { get; set; }
}
