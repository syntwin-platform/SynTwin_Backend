using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Admin.Dtos;

public sealed class AdminUpdateUserSubscriptionRequest
{
    [Required]
    [StringLength(50)]
    public string SubscriptionPlan { get; set; } = string.Empty;
}
