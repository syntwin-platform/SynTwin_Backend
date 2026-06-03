using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Users.Dtos;

public sealed class UpdateCurrentUserRequest
{
    [StringLength(100)]
    public string? FullName { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    [Required]
    [StringLength(50)]
    public string Timezone { get; set; } = "UTC";
}