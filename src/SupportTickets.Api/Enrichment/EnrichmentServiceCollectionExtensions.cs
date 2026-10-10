using SupportTickets.Api.Enrichment.Mock;

namespace SupportTickets.Api.Enrichment;

public static class EnrichmentServiceCollectionExtensions
{
    public static IServiceCollection AddTicketEnrichment(this IServiceCollection services)
    {
        services.AddOptions<EnrichmentOptions>()
            .BindConfiguration(EnrichmentOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MockProviderOptions>()
            .BindConfiguration(MockProviderOptions.SectionName);

        services.AddSingleton<EnrichmentResultParser>();
        services.AddSingleton<EnrichmentRetryPolicy>();
        services.AddScoped<EnrichmentProcessor>();
        services.AddHostedService<EnrichmentWorker>();

        // The only provider today. A real LLM client plugs in here behind IEnrichmentProvider.
        services.AddSingleton<MockEnrichmentProvider>();
        services.AddSingleton<IEnrichmentProvider>(sp => sp.GetRequiredService<MockEnrichmentProvider>());

        return services;
    }
}
