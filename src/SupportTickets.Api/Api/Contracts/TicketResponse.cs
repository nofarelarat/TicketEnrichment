using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Api.Contracts;

public class TicketResponse
{
    public string Id { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public EnrichmentResponse Enrichment { get; set; } = new();

    public static TicketResponse From(Ticket ticket) => new()
    {
        Id = ticket.Id,
        Subject = ticket.Subject,
        Message = ticket.Message,
        CreatedAt = ticket.CreatedAt,
        Enrichment = new EnrichmentResponse
        {
            Status = ticket.EnrichmentStatus,
            Category = ticket.Category,
            Priority = ticket.Priority,
            Summary = ticket.Summary,
            Attempts = ticket.AttemptCount,
            EnrichedAt = ticket.EnrichedAt
        }
    };
}
