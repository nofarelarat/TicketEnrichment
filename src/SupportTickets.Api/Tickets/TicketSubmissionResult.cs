using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Tickets;

public class TicketSubmissionResult
{
    public TicketSubmissionResult(SubmissionOutcome outcome, Ticket ticket)
    {
        Outcome = outcome;
        Ticket = ticket;
    }

    public SubmissionOutcome Outcome { get; }

    /// <summary>The stored ticket: the new one, or the existing one for a duplicate or conflict.</summary>
    public Ticket Ticket { get; }
}
