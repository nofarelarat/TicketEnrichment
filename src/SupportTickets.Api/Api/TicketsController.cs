using Microsoft.AspNetCore.Mvc;
using SupportTickets.Api.Api.Contracts;
using SupportTickets.Api.Tickets;

namespace SupportTickets.Api.Api;

[ApiController]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly TicketIntakeService _intake;

    public TicketsController(TicketIntakeService intake)
    {
        _intake = intake;
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
                return AcceptedAtAction(nameof(Get), new { id = response.Id }, response);

            case SubmissionOutcome.Duplicate:
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

        return Ok(TicketResponse.From(ticket));
    }
}
