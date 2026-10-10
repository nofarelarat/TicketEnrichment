using SupportTickets.Api;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Enrichment.Claude;

var builder = WebApplication.CreateBuilder(args);
var startup = new Startup();

startup.ConfigureServices(builder.Services);

var app = builder.Build();

// `dotnet run --project src/SupportTickets.Api -- --claude-smoke-test` sends one sample ticket to
// the real Claude API and exits. It is the only check that needs ANTHROPIC_API_KEY, which is why
// it lives here and not in the automated tests.
if (args.Contains("--claude-smoke-test"))
{
    return await RunClaudeSmokeTestAsync(app.Services);
}

startup.Configure(app);

app.Run();
return 0;

static async Task<int> RunClaudeSmokeTestAsync(IServiceProvider services)
{
    try
    {
        var provider = services.GetRequiredService<ClaudeEnrichmentProvider>();
        var parser = services.GetRequiredService<EnrichmentResultParser>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var rawOutput = await provider.RequestEnrichmentAsync(
            "Payment taken twice",
            "Urgent: my payment was taken twice this month.",
            timeout.Token);
        var enrichment = parser.Parse(rawOutput);

        Console.WriteLine($"Raw output: {rawOutput}");
        Console.WriteLine($"Category:   {enrichment.Category}");
        Console.WriteLine($"Priority:   {enrichment.Priority}");
        Console.WriteLine($"Summary:    {enrichment.Summary}");
        Console.WriteLine("Claude smoke test PASSED.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Claude smoke test FAILED: {ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}
