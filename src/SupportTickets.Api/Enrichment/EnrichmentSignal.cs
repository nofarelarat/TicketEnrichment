namespace SupportTickets.Api.Enrichment;

/// <summary>
/// Lets intake wake the worker as soon as a ticket is stored, so enrichment does not wait for
/// the next poll. It is only a latency optimisation: the database is the queue, and a missed
/// signal is covered by polling.
/// </summary>
public class EnrichmentSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled; one pending wake-up is enough.
        }
    }

    /// <summary>Completes when signalled or when <paramref name="timeout"/> elapses.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);
}
