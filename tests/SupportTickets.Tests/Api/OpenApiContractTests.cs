using System.Net.Http.Json;
using System.Text.Json;
using SupportTickets.Api.Domain;
using SupportTickets.Tests.Infrastructure;

namespace SupportTickets.Tests.Api;

public class OpenApiContractTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("EnrichmentStatus", typeof(EnrichmentStatus))]
    [InlineData("TicketCategory", typeof(TicketCategory))]
    [InlineData("TicketPriority", typeof(TicketPriority))]
    public async Task Contract_DescribesEnumsAsTheStringsTheApiReturns(string schemaName, Type enumType)
    {
        var contract = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var documented = contract
            .GetProperty("components").GetProperty("schemas").GetProperty(schemaName).GetProperty("enum")
            .EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString());

        Assert.Equal(Enum.GetNames(enumType), documented);
    }
}
