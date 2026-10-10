using SupportTickets.Api.Domain;
using SupportTickets.Api.Enrichment;

namespace SupportTickets.Tests.Enrichment;

public class EnrichmentResultParserTests
{
    private readonly EnrichmentResultParser _parser = new();

    [Fact]
    public void Parse_ValidOutput_ReturnsEnrichment()
    {
        var result = _parser.Parse(
            """{ "category": "Billing", "priority": "High", "summary": " Customer was charged twice. " }""");

        Assert.Equal(TicketCategory.Billing, result.Category);
        Assert.Equal(TicketPriority.High, result.Priority);
        Assert.Equal("Customer was charged twice.", result.Summary);
    }

    [Fact]
    public void Parse_EnumValuesInDifferentCase_AreAccepted()
    {
        var result = _parser.Parse("""{ "category": "featurerequest", "priority": "URGENT", "summary": "s" }""");

        Assert.Equal(TicketCategory.FeatureRequest, result.Category);
        Assert.Equal(TicketPriority.Urgent, result.Priority);
    }

    [Fact]
    public void Parse_OutputWrappedInMarkdownCodeFence_IsAccepted()
    {
        var result = _parser.Parse(
            "```json\n{ \"category\": \"Bug\", \"priority\": \"Low\", \"summary\": \"It broke.\" }\n```");

        Assert.Equal(TicketCategory.Bug, result.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sure! Here is the classification you asked for.")]
    [InlineData("{ \"category\": \"Bug\", \"priority\": ")]
    [InlineData("[]")]
    [InlineData("\"Bug\"")]
    [InlineData("```")]
    public void Parse_NotAJsonObject_IsRejected(string rawOutput)
    {
        Assert.Throws<InvalidEnrichmentOutputException>(() => _parser.Parse(rawOutput));
    }

    [Theory]
    // Missing fields.
    [InlineData("""{ "priority": "Low", "summary": "s" }""")]
    [InlineData("""{ "category": "Bug", "summary": "s" }""")]
    [InlineData("""{ "category": "Bug", "priority": "Low" }""")]
    // Wrong types.
    [InlineData("""{ "category": 1, "priority": "Low", "summary": "s" }""")]
    [InlineData("""{ "category": "Bug", "priority": "Low", "summary": null }""")]
    // Values outside the taxonomy.
    [InlineData("""{ "category": "Complaint", "priority": "Low", "summary": "s" }""")]
    [InlineData("""{ "category": "Bug", "priority": "Critical", "summary": "s" }""")]
    // Enum.TryParse would accept these; the parser must not.
    [InlineData("""{ "category": "1", "priority": "Low", "summary": "s" }""")]
    [InlineData("""{ "category": "Bug,Billing", "priority": "Low", "summary": "s" }""")]
    // Empty summary.
    [InlineData("""{ "category": "Bug", "priority": "Low", "summary": "   " }""")]
    public void Parse_OffSchemaOutput_IsRejected(string rawOutput)
    {
        Assert.Throws<InvalidEnrichmentOutputException>(() => _parser.Parse(rawOutput));
    }

    [Fact]
    public void Parse_SummaryOverTheLimit_IsRejected()
    {
        var summary = new string('x', TicketLimits.SummaryMaxLength + 1);

        Assert.Throws<InvalidEnrichmentOutputException>(() =>
            _parser.Parse($$"""{ "category": "Bug", "priority": "Low", "summary": "{{summary}}" }"""));
    }
}
