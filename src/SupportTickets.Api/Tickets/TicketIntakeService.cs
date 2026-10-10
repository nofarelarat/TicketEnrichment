using Microsoft.EntityFrameworkCore;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Persistence;

namespace SupportTickets.Api.Tickets;

/// <summary>
/// Stores and reads tickets. Intake never calls the AI provider: it persists the ticket as
/// pending and wakes the enrichment worker, so provider failures cannot fail a submission.
/// </summary>
public class TicketIntakeService
{
    private readonly TicketDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly EnrichmentSignal _signal;

    public TicketIntakeService(TicketDbContext db, TimeProvider timeProvider, EnrichmentSignal signal)
    {
        _db = db;
        _timeProvider = timeProvider;
        _signal = signal;
    }

    public async Task<TicketSubmissionResult> SubmitAsync(
        string id, string subject, string message, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(id, cancellationToken);
        if (existing is not null)
        {
            return ResultForExisting(existing, subject, message);
        }

        var ticket = new Ticket(id, subject, message, _timeProvider.GetUtcNow());
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

        _signal.Notify();
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
