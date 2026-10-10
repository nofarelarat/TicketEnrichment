using System.ComponentModel.DataAnnotations;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Api.Contracts;

public class CreateTicketRequest
{
    /// <summary>Client-supplied ticket id. Also the idempotency key for submissions.</summary>
    [Required]
    [StringLength(TicketLimits.IdMaxLength)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]*$",
        ErrorMessage = "The id may contain only letters, digits, '.', '_' and '-', and must start with a letter or digit.")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.SubjectMaxLength)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;
}
