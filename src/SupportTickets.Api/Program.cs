using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Enrichment.Claude;
using SupportTickets.Api.Persistence;
using SupportTickets.Api.Tickets;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddDbContext<TicketDbContext>((serviceProvider, options) =>
    options.UseSqlite(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Tickets")));
builder.Services.AddScoped<TicketService>();
builder.Services.AddTicketEnrichment();

var app = builder.Build();

// `dotnet run --project src/SupportTickets.Api -- --claude-smoke-test` sends one sample ticket to
// the real Claude API and exits. It is the only check that needs ANTHROPIC_API_KEY, which is why
// it lives here and not in the automated tests.
if (args.Contains("--claude-smoke-test"))
{
    return await RunClaudeSmokeTestAsync(app.Services);
}

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TicketDbContext>().Database.EnsureCreated();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapControllers();

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
            "Charged twice this month",
            "I was charged twice for my subscription and need a refund as soon as possible.",
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
