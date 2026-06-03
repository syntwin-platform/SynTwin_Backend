using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Admin.Dtos;

public sealed class AdminUpdateUserStatusRequest
{
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;
}