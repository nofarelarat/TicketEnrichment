using System.ComponentModel.DataAnnotations;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Api.Contracts;

public class CreateTicketRequest
{
    /// <summary>Client-supplied ticket id, digits only. Also the idempotency key for submissions.</summary>
    [Required]
    [StringLength(TicketLimits.IdMaxLength)]
    [RegularExpression("^[0-9]+$", ErrorMessage = "The id may contain only digits.")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.SubjectMaxLength)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;
}
