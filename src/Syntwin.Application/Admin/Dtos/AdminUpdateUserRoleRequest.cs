using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Admin.Dtos;

public sealed class AdminUpdateUserRoleRequest
{
    [Required]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;
}