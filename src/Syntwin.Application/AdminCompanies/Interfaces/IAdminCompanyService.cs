using Syntwin.Application.AdminCompanies.Dtos;

namespace Syntwin.Application.AdminCompanies.Interfaces;

public interface IAdminCompanyService
{
    Task<IReadOnlyList<AdminCompanyResponse>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCompanyMemberResponse>?> ListMembersAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<AdminCompanyMemberResponse?> AddMonitorAsync(
        Guid companyId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminCompanyMemberResponse?> ReplaceMonitorAsync(
        Guid companyId,
        Guid monitorUserId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveMonitorAsync(
        Guid companyId,
        Guid monitorUserId,
        CancellationToken cancellationToken = default);
}
