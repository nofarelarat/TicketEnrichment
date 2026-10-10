using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Persistence;
using SupportTickets.Api.Tickets;

namespace SupportTickets.Api;

/// <summary>Registers the application's services and builds its HTTP pipeline.</summary>
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddHealthChecks();

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<TicketDbContext>((serviceProvider, options) =>
            options.UseSqlite(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Tickets")));
        services.AddScoped<TicketService>();
        services.AddTicketEnrichment();
    }

    public void Configure(WebApplication app)
    {
        EnsureDatabaseCreated(app.Services);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapOpenApi();
        app.MapHealthChecks("/health");
        app.MapControllers();
    }

    private static void EnsureDatabaseCreated(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TicketDbContext>().Database.EnsureCreated();
    }
}
