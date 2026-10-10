using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportTickets.Api.Domain;
using SupportTickets.Tests.Infrastructure;

namespace SupportTickets.Tests.Api;

public class TicketsApiTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();
    private readonly TicketApiClient _api;

    public TicketsApiTests()
    {
        _api = new TicketApiClient(_factory.CreateClient());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Submit_ValidTicket_Returns202WithLocationAndPendingEnrichment()
    {
        var response = await _api.SubmitAsync("T-1", "Charged twice", "I was charged twice this month.");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.EndsWith("/api/v1/tickets/T-1", response.Headers.Location!.ToString());

        var ticket = await TicketApiClient.ReadTicketAsync(response);
        Assert.Equal("T-1", ticket.Id);
        Assert.Equal("Charged twice", ticket.Subject);
        Assert.Equal(_factory.Time.GetUtcNow(), ticket.CreatedAt);
        Assert.Equal(EnrichmentStatus.Pending, ticket.Enrichment.Status);
        Assert.Null(ticket.Enrichment.Category);
        Assert.Null(ticket.Enrichment.Priority);
        Assert.Null(ticket.Enrichment.Summary);
    }

    [Fact]
    public async Task Submit_SameTicketTwice_IsIdempotentAndReturns200()
    {
        await _api.SubmitAsync("T-1", "Charged twice", "I was charged twice.");

        var replay = await _api.SubmitAsync("T-1", "Charged twice", "I was charged twice.");

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("T-1", (await TicketApiClient.ReadTicketAsync(replay)).Id);
    }

    [Fact]
    public async Task Submit_SameIdWithDifferentContent_Returns409ProblemAndKeepsOriginal()
    {
        await _api.SubmitAsync("T-1", "Charged twice", "I was charged twice.");

        var conflict = await _api.SubmitAsync("T-1", "Something else", "A different message.");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Charged twice", (await _api.GetTicketAsync("T-1")).Subject);
    }

    [Theory]
    [InlineData("", "subject", "message")]
    [InlineData("has spaces", "subject", "message")]
    [InlineData("../etc", "subject", "message")]
    [InlineData("T-1", "", "message")]
    [InlineData("T-1", "subject", "   ")]
    public async Task Submit_InvalidPayload_Returns400ValidationProblem(string id, string subject, string message)
    {
        var response = await _api.SubmitAsync(id, subject, message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Submit_OversizedMessage_Returns400()
    {
        var response = await _api.SubmitAsync("T-1", "subject", new string('x', TicketLimits.MessageMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownTicket_Returns404Problem()
    {
        var response = await _api.GetAsync("does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Get_PendingTicket_Returns200WithRetryAfterHint()
    {
        await _api.SubmitAsync("T-1", "Charged twice", "I was charged twice.");

        var response = await _api.GetAsync("T-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task Get_EnrichedTicket_ReturnsCategoryPriorityAndSummary()
    {
        await _api.SubmitAsync("T-1", "Refund needed", "Urgent: please refund my last invoice.");

        await _factory.RunEnrichmentPassAsync();
        var response = await _api.GetAsync("T-1");
        var ticket = await TicketApiClient.ReadTicketAsync(response);

        Assert.Null(response.Headers.RetryAfter);
        Assert.Equal(EnrichmentStatus.Completed, ticket.Enrichment.Status);
        Assert.Equal(TicketCategory.Billing, ticket.Enrichment.Category);
        Assert.Equal(TicketPriority.Urgent, ticket.Enrichment.Priority);
        Assert.Equal("Customer reports: Refund needed.", ticket.Enrichment.Summary);
        Assert.Equal(1, ticket.Enrichment.Attempts);
        Assert.Equal(_factory.Time.GetUtcNow(), ticket.Enrichment.EnrichedAt);
    }
}
