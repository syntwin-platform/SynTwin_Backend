using Syntwin.Application.Companies.Dtos;

namespace Syntwin.Application.Companies.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyResponse>> ListMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CompanyResponse?> GetAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<CompanyResponse> CreateAsync(
        Guid userId,
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyResponse?> UpdateAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyMemberResponse>?> ListMembersAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<CompanyMemberResponse?> AddMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        OwnerLinkedAccountRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<CompanyMemberResponse?> ReplaceMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        OwnerLinkedAccountRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<CompanyMemberResponse?> SetMonitorStatusAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        bool isActive,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
