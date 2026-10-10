namespace SupportTickets.Api.Domain;

public enum EnrichmentStatus
{
    /// <summary>Waiting for a first attempt or for a scheduled retry.</summary>
    Pending,

    Completed,

    /// <summary>Terminal: every automatic attempt was used. The raw ticket is still stored.</summary>
    Failed
}
