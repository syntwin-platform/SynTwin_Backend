using Syntwin.Application.AdminCompanies.Dtos;
using Syntwin.Application.AdminCompanies.Interfaces;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.AdminCompanies.Services;

public sealed class AdminCompanyService : IAdminCompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserRepository _userRepository;

    public AdminCompanyService(
        ICompanyRepository companyRepository,
        IUserRepository userRepository)
    {
        _companyRepository = companyRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<AdminCompanyResponse>> ListAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var companies = await _companyRepository.ListAllAsync(
            search,
            cancellationToken);

        return companies.Select(ToCompanyResponse).ToList();
    }

    public async Task<IReadOnlyList<AdminCompanyMemberResponse>?> ListMembersAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(
            companyId,
            cancellationToken);

        if (company is null)
        {
            return null;
        }

        return company.Members
            .Where(member => member.IsActive && member.User is not null)
            .OrderBy(member => member.Role)
            .ThenBy(member => member.User!.Email)
            .Select(ToMemberResponse)
            .ToList();
    }

    public async Task<AdminCompanyMemberResponse?> AddMonitorAsync(
        Guid companyId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(
            companyId,
            cancellationToken);

        if (company is null)
        {
            return null;
        }

        var user = await GetValidMonitorUserAsync(request.Email, cancellationToken);
        var existing = company.Members.FirstOrDefault(member => member.UserId == user.Id);

        if (existing is not null)
        {
            if (existing.Role == CompanyMemberRole.Owner)
            {
                throw new InvalidOperationException(
                    "The company owner cannot also be linked as a monitor.");
            }

            if (existing.IsActive)
            {
                throw new InvalidOperationException(
                    "This account is already linked to the company.");
            }

            existing.Role = CompanyMemberRole.Monitor;
            existing.IsActive = true;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.User = user;

            await _companyRepository.SaveChangesAsync(cancellationToken);
            return ToMemberResponse(existing);
        }

        var monitor = new CompanyMember
        {
            CompanyId = companyId,
            UserId = user.Id,
            Role = CompanyMemberRole.Monitor,
            IsActive = true,
            JoinedAt = DateTimeOffset.UtcNow,
            Company = company,
            User = user
        };

        await _companyRepository.AddMemberAsync(monitor, cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(monitor);
    }

    public async Task<AdminCompanyMemberResponse?> ReplaceMonitorAsync(
        Guid companyId,
        Guid monitorUserId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(
            companyId,
            cancellationToken);

        if (company is null)
        {
            return null;
        }

        var currentMonitor = company.Members.FirstOrDefault(member =>
            member.UserId == monitorUserId &&
            member.IsActive &&
            member.Role == CompanyMemberRole.Monitor);

        if (currentMonitor is null)
        {
            return null;
        }

        var replacementUser = await GetValidMonitorUserAsync(
            request.Email,
            cancellationToken);

        if (replacementUser.Id == currentMonitor.UserId)
        {
            currentMonitor.User = replacementUser;
            return ToMemberResponse(currentMonitor);
        }

        var replacementMembership = company.Members.FirstOrDefault(
            member => member.UserId == replacementUser.Id);

        if (replacementMembership is not null && replacementMembership.IsActive)
        {
            throw new InvalidOperationException(
                "The replacement account is already linked to this company.");
        }

        currentMonitor.IsActive = false;
        currentMonitor.UpdatedAt = DateTimeOffset.UtcNow;

        if (replacementMembership is not null)
        {
            replacementMembership.Role = CompanyMemberRole.Monitor;
            replacementMembership.IsActive = true;
            replacementMembership.UpdatedAt = DateTimeOffset.UtcNow;
            replacementMembership.User = replacementUser;
        }
        else
        {
            replacementMembership = new CompanyMember
            {
                CompanyId = companyId,
                UserId = replacementUser.Id,
                Role = CompanyMemberRole.Monitor,
                IsActive = true,
                JoinedAt = DateTimeOffset.UtcNow,
                Company = company,
                User = replacementUser
            };

            await _companyRepository.AddMemberAsync(
                replacementMembership,
                cancellationToken);
        }

        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(replacementMembership);
    }

    public async Task<bool> RemoveMonitorAsync(
        Guid companyId,
        Guid monitorUserId,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(
            companyId,
            cancellationToken);

        if (company is null)
        {
            return false;
        }

        var monitor = company.Members.FirstOrDefault(member =>
            member.UserId == monitorUserId &&
            member.IsActive &&
            member.Role == CompanyMemberRole.Monitor);

        if (monitor is null)
        {
            return false;
        }

        monitor.IsActive = false;
        monitor.UpdatedAt = DateTimeOffset.UtcNow;
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<User> GetValidMonitorUserAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(
                "The email must belong to an active SynTwin account.");
        }

        if (user.Role == UserRole.SuperAdmin)
        {
            throw new InvalidOperationException(
                "A SuperAdmin account cannot be linked as a monitor.");
        }

        return user;
    }

    private static AdminCompanyResponse ToCompanyResponse(Company company)
    {
        var owner = company.Members.FirstOrDefault(member =>
            member.IsActive &&
            member.Role == CompanyMemberRole.Owner);

        return new AdminCompanyResponse
        {
            Id = company.Id,
            Name = company.Name,
            Slug = company.Slug,
            Status = company.Status.ToString(),
            OwnerUserId = owner?.UserId,
            OwnerEmail = owner?.User?.Email ?? string.Empty,
            OwnerFullName = owner?.User?.FullName,
            MonitorCount = company.Members.Count(member =>
                member.IsActive &&
                member.Role == CompanyMemberRole.Monitor),
            CreatedAt = company.CreatedAt
        };
    }

    private static AdminCompanyMemberResponse ToMemberResponse(
        CompanyMember member)
    {
        return new AdminCompanyMemberResponse
        {
            UserId = member.UserId,
            Email = member.User?.Email ?? string.Empty,
            FullName = member.User?.FullName,
            AvatarUrl = member.User?.AvatarUrl,
            Role = member.Role.ToString(),
            JoinedAt = member.JoinedAt
        };
    }
}
