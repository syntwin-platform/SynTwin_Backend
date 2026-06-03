namespace Syntwin.Application.Admin.Dtos;

public sealed class AdminUserListResponse
{
    public IReadOnlyList<AdminUserListItemResponse> Items { get; set; } = Array.Empty<AdminUserListItemResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
