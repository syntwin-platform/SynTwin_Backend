using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly SyntwinDbContext _dbContext;

    public CompanyRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CompanyMember>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CompanyMembers
            .Include(member => member.User)
            .Include(member => member.Company)
                .ThenInclude(company => company!.Members)
                .ThenInclude(member => member.User)
            .Include(member => member.Company)
                .ThenInclude(company => company!.CreatedByUser)
                .ThenInclude(owner => owner!.Subscriptions
                    .Where(subscription =>
                        subscription.Status == SubscriptionStatus.Active))
                .ThenInclude(subscription => subscription.Plan)
            .Where(member =>
                member.UserId == userId &&
                member.IsActive)
            .OrderBy(member => member.Company!.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Company>> ListAllAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Companies
            .Include(company => company.Members)
                .ThenInclude(member => member.User)
            .Include(company => company.CreatedByUser)
                .ThenInclude(owner => owner!.Subscriptions
                    .Where(subscription =>
                        subscription.Status == SubscriptionStatus.Active))
                .ThenInclude(subscription => subscription.Plan)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(company =>
                company.Name.Contains(keyword) ||
                company.Slug.Contains(keyword) ||
                company.Members.Any(member =>
                    member.User != null &&
                    (member.User.Email.Contains(keyword) ||
                     (member.User.FullName != null &&
                      member.User.FullName.Contains(keyword)))));
        }

        return await query
            .OrderByDescending(company => company.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Company?> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Companies
            .Include(company => company.Members)
                .ThenInclude(member => member.User)
            .Include(company => company.CreatedByUser)
                .ThenInclude(owner => owner!.Subscriptions
                    .Where(subscription =>
                        subscription.Status == SubscriptionStatus.Active))
                .ThenInclude(subscription => subscription.Plan)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                company => company.Id == companyId,
                cancellationToken);
    }

    public Task<CompanyMember?> GetMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CompanyMembers
            .Include(member => member.Company)
                .ThenInclude(company => company!.Members)
                .ThenInclude(member => member.User)
            .Include(member => member.Company)
                .ThenInclude(company => company!.CreatedByUser)
                .ThenInclude(owner => owner!.Subscriptions
                    .Where(subscription =>
                        subscription.Status == SubscriptionStatus.Active))
                .ThenInclude(subscription => subscription.Plan)
            .Include(member => member.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                member =>
                    member.CompanyId == companyId &&
                    member.UserId == userId &&
                    member.IsActive,
                cancellationToken);
    }

    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludedCompanyId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Companies.AnyAsync(
            company =>
                company.Slug == slug &&
                (!excludedCompanyId.HasValue ||
                 company.Id != excludedCompanyId.Value),
            cancellationToken);
    }

    public async Task AddCompanyAsync(
        Company company,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Companies.AddAsync(
            company,
            cancellationToken);
    }

    public async Task AddMemberAsync(
        CompanyMember member,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.CompanyMembers.AddAsync(
            member,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}