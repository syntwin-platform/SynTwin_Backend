namespace Syntwin.Application.AdminCompanies.Dtos;

public sealed class AdminCompanyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? OwnerUserId { get; set; }

    public string OwnerEmail { get; set; } = string.Empty;

    public string? OwnerFullName { get; set; }

    public int MonitorCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
