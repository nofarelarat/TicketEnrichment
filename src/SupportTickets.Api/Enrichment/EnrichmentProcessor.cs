using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Persistence;

namespace SupportTickets.Api.Enrichment;

/// <summary>
/// Runs one pass over the tickets that are due for enrichment. Every outcome of an attempt,
/// success or failure, is written back to the ticket row, so a crash or restart never loses
/// a ticket: it simply stays pending and is picked up again (at-least-once).
/// </summary>
public class EnrichmentProcessor
{
    private readonly TicketDbContext _db;
    private readonly IEnrichmentProvider _provider;
    private readonly EnrichmentResultParser _parser;
    private readonly EnrichmentRetryPolicy _retryPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly EnrichmentOptions _options;
    private readonly ILogger<EnrichmentProcessor> _logger;

    public EnrichmentProcessor(
        TicketDbContext db,
        IEnrichmentProvider provider,
        EnrichmentResultParser parser,
        EnrichmentRetryPolicy retryPolicy,
        TimeProvider timeProvider,
        IOptions<EnrichmentOptions> options,
        ILogger<EnrichmentProcessor> logger)
    {
        _db = db;
        _provider = provider;
        _parser = parser;
        _retryPolicy = retryPolicy;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EnrichmentBatchResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var dueTickets = await _db.Tickets
            .Where(t => t.EnrichmentStatus == EnrichmentStatus.Pending && t.NextAttemptAt <= now)
            .OrderBy(t => t.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var attempted = 0;
        var completed = 0;
        var providerUnhealthy = false;

        foreach (var ticket in dueTickets)
        {
            var outcome = await AttemptAsync(ticket, cancellationToken);

            // Saved per ticket so progress survives a crash later in the batch.
            await _db.SaveChangesAsync(cancellationToken);
            attempted++;

            if (outcome == AttemptOutcome.Completed)
            {
                completed++;
            }
            else if (outcome == AttemptOutcome.ProviderFailure)
            {
                // The provider itself is in trouble, so the rest of the batch would fail the same
                // way and burn its attempts. Stop here and let the next pass try again.
                providerUnhealthy = true;
                break;
            }
        }

        var moreWorkLikely = !providerUnhealthy && dueTickets.Count == _options.BatchSize;
        return new EnrichmentBatchResult(attempted, completed, providerUnhealthy, moreWorkLikely);
    }

    private async Task<AttemptOutcome> AttemptAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptTimeout.CancelAfter(_options.AttemptTimeout);

        try
        {
            var rawOutput = await _provider.RequestEnrichmentAsync(ticket.Subject, ticket.Message, attemptTimeout.Token);
            var enrichment = _parser.Parse(rawOutput);

            ticket.CompleteEnrichment(enrichment, _timeProvider.GetUtcNow());
            _logger.LogInformation(
                "Ticket {TicketId} enriched on attempt {Attempt}.", ticket.Id, ticket.AttemptCount);
            return AttemptOutcome.Completed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(ticket, $"Provider did not respond within {_options.AttemptTimeout}.", retryAfter: null);
            return AttemptOutcome.ProviderFailure;
        }
        catch (EnrichmentProviderException ex)
        {
            RecordFailure(ticket, ex.Message, ex.RetryAfter);
            return AttemptOutcome.ProviderFailure;
        }
        catch (InvalidEnrichmentOutputException ex)
        {
            // Bad output is specific to this response, not a sign the provider is down.
            RecordFailure(ticket, ex.Message, retryAfter: null);
            return AttemptOutcome.InvalidOutput;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error while enriching ticket {TicketId}.", ticket.Id);
            RecordFailure(ticket, $"Unexpected error: {ex.GetType().Name}.", retryAfter: null);
            return AttemptOutcome.ProviderFailure;
        }
    }

    private void RecordFailure(Ticket ticket, string error, TimeSpan? retryAfter)
    {
        var attemptsMade = ticket.AttemptCount + 1;
        var retryDelay = _retryPolicy.GetRetryDelay(attemptsMade, retryAfter);
        var retryAt = retryDelay is null ? (DateTimeOffset?)null : _timeProvider.GetUtcNow() + retryDelay.Value;

        ticket.RecordFailedAttempt(error, retryAt);

        if (retryAt is null)
        {
            _logger.LogError(
                "Ticket {TicketId} enrichment failed permanently after {Attempt} attempts: {Error}",
                ticket.Id, attemptsMade, error);
        }
        else
        {
            _logger.LogWarning(
                "Ticket {TicketId} enrichment attempt {Attempt} failed, retrying at {RetryAt}: {Error}",
                ticket.Id, attemptsMade, retryAt, error);
        }
    }

    private enum AttemptOutcome
    {
        Completed,
        InvalidOutput,
        ProviderFailure
    }
}
