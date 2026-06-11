using Syntwin.Domain.Entities;

namespace Syntwin.Application.Companies.Interfaces;

public interface ICompanyRepository
{
    Task<IReadOnlyList<CompanyMember>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Company>> ListAllAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<Company?> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<CompanyMember?> GetMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludedCompanyId = null,
        CancellationToken cancellationToken = default);

    Task AddCompanyAsync(
        Company company,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        CompanyMember member,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
