namespace SupportTickets.Api.Enrichment.Mock;

public class MockProviderOptions
{
    public const string SectionName = "Enrichment:Mock";

    public MockFailureMode FailureMode { get; set; } = MockFailureMode.None;

    /// <summary>
    /// How many calls fail before the mock starts answering correctly, to show recovery.
    /// Null means it fails on every call.
    /// </summary>
    public int? FailuresBeforeSuccess { get; set; }
}
