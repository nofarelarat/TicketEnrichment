namespace SupportTickets.Api.Enrichment.Claude;

public class ClaudeProviderOptions
{
    public const string SectionName = "Enrichment:Claude";

    /// <summary>The environment variable the API key is read from. The key is never stored in configuration.</summary>
    public const string ApiKeyEnvironmentVariable = "ANTHROPIC_API_KEY";

    public string Model { get; set; } = "claude-opus-5-5";

    public long MaxTokens { get; set; } = 1024;
}
