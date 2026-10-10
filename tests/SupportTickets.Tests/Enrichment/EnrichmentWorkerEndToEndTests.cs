using System.Diagnostics;
using System.Net;
using SupportTickets.Api.Api.Contracts;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Enrichment.Mock;
using SupportTickets.Tests.Infrastructure;

namespace SupportTickets.Tests.Enrichment;

/// <summary>Runs the real background worker on the real clock; everything else drives passes by hand.</summary>
public class EnrichmentWorkerEndToEndTests : IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    private readonly TicketApiFactory _factory = new TicketApiFactory().WithRunningWorker();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SubmittedTicket_IsEnrichedInTheBackground()
    {
        var api = new TicketApiClient(_factory.CreateClient());

        var submit = await api.SubmitAsync("100001", "Cannot login", "I cannot login to my account.");
        var ticket = await WaitForEnrichmentAsync(api, "100001");

        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        Assert.Equal(EnrichmentStatus.Completed, ticket.Enrichment.Status);
        Assert.Equal(TicketCategory.Account, ticket.Enrichment.Category);
        Assert.Equal(TicketPriority.High, ticket.Enrichment.Priority);
    }

    [Fact]
    public async Task ProviderOutage_WorkerRetriesUntilTheProviderRecovers()
    {
        _factory.WithProviderFailure(MockFailureMode.Outage, failuresBeforeSuccess: 2);
        var api = new TicketApiClient(_factory.CreateClient());

        await api.SubmitAsync("100001", "Cannot login", "I cannot login to my account.");
        var ticket = await WaitForEnrichmentAsync(api, "100001");

        Assert.Equal(EnrichmentStatus.Completed, ticket.Enrichment.Status);
        Assert.Equal(3, ticket.Enrichment.Attempts);
    }

    private static async Task<TicketResponse> WaitForEnrichmentAsync(TicketApiClient api, string id)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var ticket = await api.GetTicketAsync(id);
            if (ticket.Enrichment.Status != EnrichmentStatus.Pending || stopwatch.Elapsed > Deadline)
            {
                return ticket;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }
    }
}
