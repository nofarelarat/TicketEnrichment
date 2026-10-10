using System.Text.Json;
using Microsoft.Extensions.Options;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Enrichment.Mock;

/// <summary>
/// Stands in for a real LLM so the service runs and is tested without credentials. It answers
/// deterministically from keywords, and can be configured to fail the way real providers do.
/// </summary>
public class MockEnrichmentProvider : IEnrichmentProvider
{
    private static readonly (string Keyword, TicketCategory Category)[] CategoryKeywords =
    [
        ("payment", TicketCategory.Billing),
        ("password", TicketCategory.Account),
        ("login", TicketCategory.Account),
        ("account", TicketCategory.Account),
        ("feature", TicketCategory.FeatureRequest),
        ("would be great", TicketCategory.FeatureRequest),
        ("please add", TicketCategory.FeatureRequest)
    ];

    private static readonly (string Keyword, TicketPriority Priority)[] PriorityKeywords =
    [
        ("urgent", TicketPriority.Urgent),
        ("asap", TicketPriority.Urgent),
        ("outage", TicketPriority.Urgent),
        ("cannot", TicketPriority.High),
        ("can't", TicketPriority.High),
        ("broken", TicketPriority.High),
        ("whenever", TicketPriority.Low),
        ("no rush", TicketPriority.Low)
    ];

    private readonly MockProviderOptions _options;
    private int _callCount;

    public MockEnrichmentProvider(IOptions<MockProviderOptions> options)
    {
        _options = options.Value;
    }

    public int CallCount => _callCount;

    public async Task<string> RequestEnrichmentAsync(string subject, string message, CancellationToken cancellationToken)
    {
        var callNumber = Interlocked.Increment(ref _callCount); // adding lock around _callCount++

        if (!ShouldFail(callNumber))
        {
            return BuildValidOutput(subject, message);
        }

        switch (_options.FailureMode)
        {
            case MockFailureMode.Timeout:
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable: the delay only ends by cancellation.");

            case MockFailureMode.Outage:
                throw new EnrichmentProviderException("Mock provider is unavailable (HTTP 503).");

            case MockFailureMode.RateLimited:
                throw new EnrichmentProviderException("Mock provider is rate limiting (HTTP 429).");

            case MockFailureMode.MalformedJson:
                return "{ \"category\": \"Bug\", \"priority\": ";

            case MockFailureMode.OffSchema:
                return "{ \"category\": \"Complaint\", \"priority\": \"Critical\", \"summary\": \"\" }";

            default:
                throw new InvalidOperationException($"Unhandled mock failure mode '{_options.FailureMode}'.");
        }
    }

    // for testing, so we can assert that the provider was called the expected number of times
    private bool ShouldFail(int callNumber)
    {
        if (_options.FailureMode == MockFailureMode.None)
        {
            return false;
        }

        return _options.FailuresBeforeSuccess is null || callNumber <= _options.FailuresBeforeSuccess;
    }

    private static string BuildValidOutput(string subject, string message)
    {
        var text = $"{subject} {message}";
        var category = Match(text, CategoryKeywords, TicketCategory.Bug);
        var priority = Match(text, PriorityKeywords, TicketPriority.Medium);
        var summary = $"Customer reports: {subject.Trim().TrimEnd('.')}.";

        return JsonSerializer.Serialize(new
        {
            category = category.ToString(),
            priority = priority.ToString(),
            summary
        });
    }

    private static TValue Match<TValue>(string text, (string Keyword, TValue Value)[] keywords, TValue fallback)
    {
        foreach (var (keyword, value) in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return fallback;
    }
}
