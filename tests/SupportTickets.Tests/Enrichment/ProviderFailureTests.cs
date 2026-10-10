using System.Net;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Enrichment.Mock;
using SupportTickets.Tests.Infrastructure;

namespace SupportTickets.Tests.Enrichment;

/// <summary>
/// The core promise of the service: whatever the AI provider does, submissions succeed,
/// tickets are kept, and reads keep working.
/// </summary>
public class ProviderFailureTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(MockFailureMode.Timeout)]
    [InlineData(MockFailureMode.Outage)]
    [InlineData(MockFailureMode.RateLimited)]
    [InlineData(MockFailureMode.MalformedJson)]
    [InlineData(MockFailureMode.OffSchema)]
    public async Task ProviderFailsOnce_CustomerSeesNoError_AndTicketIsEnrichedOnRetry(MockFailureMode mode)
    {
        _factory
            .WithProviderFailure(mode, failuresBeforeSuccess: 1)
            .WithSetting("Enrichment:AttemptTimeout", "00:00:00.100");
        var api = CreateApi();

        var submit = await api.SubmitAsync("T-1", "App crashes", "The app crashes on start.");
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);

        await _factory.RunEnrichmentPassAsync();

        var afterFailure = await api.GetTicketAsync("T-1");
        Assert.Equal(EnrichmentStatus.Pending, afterFailure.Enrichment.Status);
        Assert.Equal(1, afterFailure.Enrichment.Attempts);
        Assert.Null(afterFailure.Enrichment.Category);
        Assert.Equal("The app crashes on start.", afterFailure.Message);

        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        await _factory.RunEnrichmentPassAsync();

        var afterRetry = await api.GetTicketAsync("T-1");
        Assert.Equal(EnrichmentStatus.Completed, afterRetry.Enrichment.Status);
        Assert.Equal(2, afterRetry.Enrichment.Attempts);
        Assert.Equal(TicketCategory.Bug, afterRetry.Enrichment.Category);
        Assert.NotNull(afterRetry.Enrichment.Priority);
        Assert.False(string.IsNullOrWhiteSpace(afterRetry.Enrichment.Summary));
    }

    [Fact]
    public async Task ProviderNeverRecovers_TicketEndsFailed_ButIsKeptAndRetrievable()
    {
        _factory
            .WithProviderFailure(MockFailureMode.Outage)
            .WithSetting("Enrichment:MaxAttempts", "3");
        var api = CreateApi();
        await api.SubmitAsync("T-1", "App crashes", "The app crashes on start.");

        for (var pass = 0; pass < 10; pass++)
        {
            await _factory.RunEnrichmentPassAsync();
            _factory.Time.Advance(TimeSpan.FromMinutes(10));
        }

        var response = await api.GetAsync("T-1");
        var ticket = await TicketApiClient.ReadTicketAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(EnrichmentStatus.Failed, ticket.Enrichment.Status);
        Assert.Equal(3, ticket.Enrichment.Attempts);
        Assert.Equal("App crashes", ticket.Subject);
        Assert.Equal("The app crashes on start.", ticket.Message);
        Assert.Equal(3, _factory.MockProvider.CallCount);
    }

    [Fact]
    public async Task FailedAttempt_IsNotRetriedUntilBackoffElapses()
    {
        _factory.WithProviderFailure(MockFailureMode.Outage);
        var api = CreateApi();
        await api.SubmitAsync("T-1", "App crashes", "The app crashes on start.");

        await _factory.RunEnrichmentPassAsync();
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(1, _factory.MockProvider.CallCount);

        // Backoff is 2s after the first failure, then 4s after the second (jitter is off in tests).
        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(2, _factory.MockProvider.CallCount);

        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(2, _factory.MockProvider.CallCount);

        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(3, _factory.MockProvider.CallCount);
    }

    [Fact]
    public async Task RateLimited_WaitsForTheProvidersRetryAfter()
    {
        _factory
            .WithProviderFailure(MockFailureMode.RateLimited)
            .WithSetting("Enrichment:Mock:RateLimitRetryAfter", "00:00:30");
        var api = CreateApi();
        await api.SubmitAsync("T-1", "App crashes", "The app crashes on start.");

        await _factory.RunEnrichmentPassAsync();

        // Our own backoff (2s) has long passed, but the provider asked for 30s.
        _factory.Time.Advance(TimeSpan.FromSeconds(29));
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(1, _factory.MockProvider.CallCount);

        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RunEnrichmentPassAsync();
        Assert.Equal(2, _factory.MockProvider.CallCount);
    }

    [Fact]
    public async Task ProviderOutage_StopsTheBatch_SoOtherTicketsKeepTheirAttempts()
    {
        _factory.WithProviderFailure(MockFailureMode.Outage);
        var api = CreateApi();
        await api.SubmitAsync("T-1", "First", "First message.");
        await api.SubmitAsync("T-2", "Second", "Second message.");
        await api.SubmitAsync("T-3", "Third", "Third message.");

        var result = await _factory.RunEnrichmentPassAsync();

        Assert.True(result.ProviderUnhealthy);
        Assert.Equal(1, result.Attempted);
        Assert.Equal(1, _factory.MockProvider.CallCount);
        Assert.Equal(0, (await api.GetTicketAsync("T-2")).Enrichment.Attempts);
        Assert.Equal(0, (await api.GetTicketAsync("T-3")).Enrichment.Attempts);
    }

    [Fact]
    public async Task InvalidOutput_DoesNotStopTheBatch()
    {
        _factory.WithProviderFailure(MockFailureMode.OffSchema, failuresBeforeSuccess: 1);
        var api = CreateApi();
        await api.SubmitAsync("T-1", "First", "First message.");
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await api.SubmitAsync("T-2", "Second", "Second message.");

        var result = await _factory.RunEnrichmentPassAsync();

        Assert.False(result.ProviderUnhealthy);
        Assert.Equal(2, result.Attempted);
        Assert.Equal(1, result.Completed);
        Assert.Equal(EnrichmentStatus.Pending, (await api.GetTicketAsync("T-1")).Enrichment.Status);
        Assert.Equal(EnrichmentStatus.Completed, (await api.GetTicketAsync("T-2")).Enrichment.Status);
    }

    private TicketApiClient CreateApi() => new(_factory.CreateClient());
}
