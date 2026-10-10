using Microsoft.Extensions.Options;
using SupportTickets.Api.Enrichment;

namespace SupportTickets.Tests.Enrichment;

public class EnrichmentRetryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void GetRetryDelay_DoublesWithEachAttempt(int attemptsMade, int expectedSeconds)
    {
        var policy = CreatePolicy();

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), policy.GetRetryDelay(attemptsMade));
    }

    [Fact]
    public void GetRetryDelay_IsCappedAtMaxRetryDelay()
    {
        var policy = CreatePolicy(options => options.MaxRetryDelay = TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(5), policy.GetRetryDelay(4));
    }

    [Fact]
    public void GetRetryDelay_NoAttemptsLeft_ReturnsNull()
    {
        var policy = CreatePolicy(options => options.MaxAttempts = 3);

        Assert.NotNull(policy.GetRetryDelay(2));
        Assert.Null(policy.GetRetryDelay(3));
    }

    [Fact]
    public void GetRetryDelay_WithJitter_StaysWithinTheConfiguredSpread()
    {
        var policy = CreatePolicy(options => options.JitterFactor = 0.2);

        for (var i = 0; i < 200; i++)
        {
            var delay = policy.GetRetryDelay(1)!.Value;
            Assert.InRange(delay, TimeSpan.FromSeconds(1.6), TimeSpan.FromSeconds(2.4));
        }
    }

    private static EnrichmentRetryPolicy CreatePolicy(Action<EnrichmentOptions>? configure = null)
    {
        var options = new EnrichmentOptions
        {
            MaxAttempts = 10,
            BaseRetryDelay = TimeSpan.FromSeconds(2),
            MaxRetryDelay = TimeSpan.FromMinutes(5),
            JitterFactor = 0
        };
        configure?.Invoke(options);

        return new EnrichmentRetryPolicy(Options.Create(options));
    }
}
