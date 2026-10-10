namespace SupportTickets.Api.Enrichment.Mock;

public enum MockFailureMode
{
    /// <summary>Always answers with valid output.</summary>
    None,

    /// <summary>Never answers; the attempt runs into the attempt timeout.</summary>
    Timeout,

    /// <summary>Fails as an unavailable provider would (HTTP 503).</summary>
    Outage,

    /// <summary>Fails as a rate-limiting provider would (HTTP 429 with Retry-After).</summary>
    RateLimited,

    /// <summary>Answers with truncated, unparseable JSON.</summary>
    MalformedJson,

    /// <summary>Answers with valid JSON whose values are outside the schema.</summary>
    OffSchema
}
