using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.AdminCompanies.Dtos;

public sealed class AdminLinkedAccountRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
}
