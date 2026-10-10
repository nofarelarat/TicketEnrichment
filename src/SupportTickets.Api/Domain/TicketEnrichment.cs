namespace SupportTickets.Api.Domain;

/// <summary>Validated AI metadata for a ticket.</summary>
public class TicketEnrichment
{
    public TicketEnrichment(TicketCategory category, TicketPriority priority, string summary)
    {
        Category = category;
        Priority = priority;
        Summary = summary;
    }

    public TicketCategory Category { get; }

    public TicketPriority Priority { get; }

    public string Summary { get; }
}
