namespace SupportTickets.Api.Domain;

/// <summary>Size limits shared by request validation, enrichment validation and the schema.</summary>
public static class TicketLimits
{
    public const int IdMaxLength = 64;
    public const int SubjectMaxLength = 200;
    public const int MessageMaxLength = 10_000;
    public const int SummaryMaxLength = 300;
}
