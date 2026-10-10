using Anthropic;
using Microsoft.Extensions.Options;
using SupportTickets.Api.Enrichment.Claude;
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

        services.AddOptions<ClaudeProviderOptions>()
            .BindConfiguration(ClaudeProviderOptions.SectionName);

        services.AddSingleton<EnrichmentResultParser>();
        services.AddSingleton<EnrichmentRetryPolicy>();
        services.AddScoped<EnrichmentProcessor>();
        services.AddHostedService<EnrichmentWorker>(); // one instance lives for the whole life of the application

        services.AddSingleton<MockEnrichmentProvider>();
        services.AddSingleton(_ => CreateAnthropicClient());
        services.AddSingleton<ClaudeEnrichmentProvider>();

        // Resolved lazily, so the Claude client (and its API key) is only needed when Claude is selected.
        services.AddSingleton<IEnrichmentProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<EnrichmentOptions>>().Value.Provider switch
            {
                EnrichmentProviderKind.Claude => serviceProvider.GetRequiredService<ClaudeEnrichmentProvider>(),
                _ => serviceProvider.GetRequiredService<MockEnrichmentProvider>()
            });

        return services;
    }

    private static AnthropicClient CreateAnthropicClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(ClaudeProviderOptions.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"The Claude provider needs the {ClaudeProviderOptions.ApiKeyEnvironmentVariable} environment variable.");
        }

        return new AnthropicClient
        {
            ApiKey = apiKey,

            // The enrichment worker owns retries and backoff; SDK retries on top would multiply them.
            MaxRetries = 0
        };
    }
}
