using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Companies.Dtos;

public sealed class CreateCompanyRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(50)]
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    [Url]
    [StringLength(500)]
    public string? LogoUrl { get; set; }
}
