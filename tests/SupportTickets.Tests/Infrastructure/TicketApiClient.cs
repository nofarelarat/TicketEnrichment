using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportTickets.Api.Api.Contracts;

namespace SupportTickets.Tests.Infrastructure;

/// <summary>Thin typed wrapper over the HTTP API so tests read as scenarios.</summary>
public class TicketApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public TicketApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<HttpResponseMessage> SubmitAsync(string id, string subject, string message) =>
        _http.PostAsJsonAsync("/api/v1/tickets", new { id, subject, message });

    public Task<HttpResponseMessage> GetAsync(string id) => _http.GetAsync($"/api/v1/tickets/{id}");

    public async Task<TicketResponse> GetTicketAsync(string id)
    {
        var response = await GetAsync(id);
        response.EnsureSuccessStatusCode();
        return await ReadTicketAsync(response);
    }

    public static async Task<TicketResponse> ReadTicketAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TicketResponse>(JsonOptions))!;
}
