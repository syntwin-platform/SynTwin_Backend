using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Companies.Dtos;

public sealed class OwnerLinkedAccountRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
}