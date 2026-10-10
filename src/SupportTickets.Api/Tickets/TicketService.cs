using Microsoft.EntityFrameworkCore;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Persistence;

namespace SupportTickets.Api.Tickets;

/// <summary>
/// Stores and reads tickets. Intake never calls the AI provider: it persists the ticket as
/// pending and the enrichment worker finds it on its next poll, so provider failures cannot
/// fail a submission.
/// </summary>
public class TicketService
{
    private readonly TicketDbContext _db;
    private readonly TimeProvider _timeProvider;

    public TicketService(TicketDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<TicketSubmissionResult> SubmitAsync(
        string id, string subject, string message, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing is not null)
        {
            return ResultForExisting(existing, subject, message);
        }

        // Timestamps are stored at millisecond precision; truncate up front so the 202 response
        // shows the same createdAt that later reads return.
        var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        var ticket = new Ticket(id, subject, message, createdAt);
        _db.Tickets.Add(ticket);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same id between the read and the insert.
            _db.Entry(ticket).State = EntityState.Detached;
            existing = await FindAsync(id, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return ResultForExisting(existing, subject, message);
        }

        return new TicketSubmissionResult(SubmissionOutcome.Created, ticket);
    }

    public Task<Ticket?> FindAsync(string id, CancellationToken cancellationToken) =>
        _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    private static TicketSubmissionResult ResultForExisting(Ticket existing, string subject, string message)
    {
        var outcome = existing.HasSameContent(subject, message)
            ? SubmissionOutcome.Duplicate
            : SubmissionOutcome.Conflict;

        return new TicketSubmissionResult(outcome, existing);
    }
}
