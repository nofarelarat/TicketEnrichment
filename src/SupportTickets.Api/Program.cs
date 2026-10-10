using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportTickets.Api.Enrichment;
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
builder.Services.AddScoped<TicketIntakeService>();
builder.Services.AddTicketEnrichment();

var app = builder.Build();

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
