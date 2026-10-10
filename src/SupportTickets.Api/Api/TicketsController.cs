using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SupportTickets.Api.Api.Contracts;
using SupportTickets.Api.Domain;
using SupportTickets.Api.Enrichment;
using SupportTickets.Api.Tickets;

namespace SupportTickets.Api.Api;

[ApiController]
[Route("api/v1/tickets")]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly TicketIntakeService _intake;
    private readonly EnrichmentOptions _enrichmentOptions;

    public TicketsController(TicketIntakeService intake, IOptions<EnrichmentOptions> enrichmentOptions)
    {
        _intake = intake;
        _enrichmentOptions = enrichmentOptions.Value;
    }

    /// <summary>Submits a ticket. Enrichment happens asynchronously; poll the Location URL for it.</summary>
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketResponse>> Submit(
        CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await _intake.SubmitAsync(request.Id, request.Subject, request.Message, cancellationToken);
        var response = TicketResponse.From(result.Ticket);

        switch (result.Outcome)
        {
            case SubmissionOutcome.Created:
                AddPollingHint(result.Ticket);
                return AcceptedAtAction(nameof(Get), new { id = response.Id }, response);

            case SubmissionOutcome.Duplicate:
                AddPollingHint(result.Ticket);
                return Ok(response);

            default:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Ticket id already used",
                    detail: $"A ticket with id '{request.Id}' already exists with a different subject or message.");
        }
    }

    /// <summary>Returns a ticket with its enrichment, whatever state the enrichment is in.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var ticket = await _intake.FindAsync(id, cancellationToken);
        if (ticket is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Ticket not found",
                detail: $"No ticket with id '{id}' exists.");
        }

        AddPollingHint(ticket);
        return Ok(TicketResponse.From(ticket));
    }

    private void AddPollingHint(Ticket ticket)
    {
        if (ticket.EnrichmentStatus == EnrichmentStatus.Pending)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(_enrichmentOptions.PollInterval.TotalSeconds));
            Response.Headers.RetryAfter = seconds.ToString();
        }
    }
}
