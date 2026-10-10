using System.Text.Json;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Enrichment;

/// <summary>
/// Turns raw model text into a validated <see cref="TicketEnrichment"/>. Model output is
/// untrusted input: anything that is not exactly the expected schema is rejected.
/// </summary>
public class EnrichmentResultParser
{
    private const string CodeFence = "```";

    /// <exception cref="InvalidEnrichmentOutputException">The text is not valid, on-schema output.</exception>
    public TicketEnrichment Parse(string rawOutput)
    {
        var json = StripCodeFence(rawOutput);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidEnrichmentOutputException("Provider output is not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidEnrichmentOutputException("Provider output is not a JSON object.");
            }

            var category = ReadEnum<TicketCategory>(root, "category");
            var priority = ReadEnum<TicketPriority>(root, "priority");
            var summary = ReadString(root, "summary").Trim();

            if (summary.Length == 0)
            {
                throw new InvalidEnrichmentOutputException("Provider output has an empty 'summary'.");
            }

            if (summary.Length > TicketLimits.SummaryMaxLength)
            {
                throw new InvalidEnrichmentOutputException(
                    $"Provider output 'summary' exceeds {TicketLimits.SummaryMaxLength} characters.");
            }

            return new TicketEnrichment(category, priority, summary);
        }
    }

    // Models often wrap JSON in a Markdown code fence even when told not to.
    private static string StripCodeFence(string rawOutput)
    {
        var text = rawOutput.Trim();
        if (!text.StartsWith(CodeFence, StringComparison.Ordinal) || !text.EndsWith(CodeFence, StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0 || firstLineEnd >= text.Length - CodeFence.Length)
        {
            return text;
        }

        return text[(firstLineEnd + 1)..^CodeFence.Length];
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidEnrichmentOutputException($"Provider output is missing a string '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement root, string propertyName)
        where TEnum : struct, Enum
    {
        var value = ReadString(root, propertyName).Trim();

        // Match on names only. Enum.TryParse would also accept numbers ("1") and
        // comma-separated lists ("Bug,Billing"), neither of which is a valid answer.
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<TEnum>(name);
            }
        }

        throw new InvalidEnrichmentOutputException(
            $"Provider output '{propertyName}' is not one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }
}
