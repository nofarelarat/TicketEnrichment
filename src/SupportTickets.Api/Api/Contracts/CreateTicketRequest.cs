using System.ComponentModel.DataAnnotations;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Api.Contracts;

public class CreateTicketRequest
{
    /// <summary>Client-supplied ticket id: exactly six digits. Also the idempotency key for submissions.</summary>
    [Required]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "The id must be exactly 6 digits.")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.SubjectMaxLength)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(TicketLimits.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;
}
