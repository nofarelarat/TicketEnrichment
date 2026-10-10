using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Enrichment.Mock;

namespace SupportTickets.Tests.Infrastructure;

/// <summary>
/// Hosts the real service against a throwaway SQLite file. By default the background worker is
/// off and time is fake, so tests drive enrichment passes and the clock themselves and stay
/// deterministic. Call <see cref="WithRunningWorker"/> for end-to-end tests.
/// </summary>
public class TicketApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"support-tickets-test-{Guid.NewGuid():N}.db");

    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Enrichment:WorkerEnabled"] = "false",
        ["Enrichment:JitterFactor"] = "0",
        ["Enrichment:BaseRetryDelay"] = "00:00:02",
        ["Enrichment:MaxAttempts"] = "5"
    };

    private bool _useFakeTime = true;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public MockEnrichmentProvider MockProvider => Services.GetRequiredService<MockEnrichmentProvider>();

    public TicketApiFactory WithSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    public TicketApiFactory WithProviderFailure(MockFailureMode mode, int? failuresBeforeSuccess = null)
    {
        _settings["Enrichment:Mock:FailureMode"] = mode.ToString();
        _settings["Enrichment:Mock:FailuresBeforeSuccess"] = failuresBeforeSuccess?.ToString();
        return this;
    }

    /// <summary>Runs the real background worker on the system clock, with short delays.</summary>
    public TicketApiFactory WithRunningWorker()
    {
        _useFakeTime = false;
        _settings["Enrichment:WorkerEnabled"] = "true";
        _settings["Enrichment:PollInterval"] = "00:00:00.050";
        _settings["Enrichment:BaseRetryDelay"] = "00:00:00.050";
        return this;
    }

    /// <summary>Runs one enrichment pass, exactly as the background worker would.</summary>
    public async Task<EnrichmentBatchResult> RunEnrichmentPassAsync()
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<EnrichmentProcessor>();
        return await processor.ProcessDueAsync(CancellationToken.None);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            var settings = new Dictionary<string, string?>(_settings)
            {
                ["ConnectionStrings:Tickets"] = $"Data Source={_databasePath}"
            };
            configuration.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            if (_useFakeTime)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Time);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }
}
