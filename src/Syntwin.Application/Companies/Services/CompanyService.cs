using System.Globalization;
using System.Text;
using System.Text.Json;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Companies.Dtos;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Companies.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public CompanyService(
        ICompanyRepository companyRepository,
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository)
    {
        _companyRepository = companyRepository;
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IReadOnlyList<CompanyResponse>> ListMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _companyRepository.ListMembershipsAsync(
            userId,
            cancellationToken);

        return memberships
            .Where(membership => membership.Company is not null)
            .Select(membership => ToCompanyResponse(membership.Company!, membership.Role))
            .ToList();
    }

    public async Task<CompanyResponse?> GetAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _companyRepository.GetMembershipAsync(
            companyId,
            userId,
            cancellationToken);

        return membership?.Company is null
            ? null
            : ToCompanyResponse(membership.Company, membership.Role);
    }

    public async Task<CompanyResponse> CreateAsync(
        Guid userId,
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Active user account is required.");
        }

        var name = request.Name.Trim();
        var slug = await CreateUniqueSlugAsync(name, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            Industry = NormalizeNullable(request.Industry),
            Address = NormalizeNullable(request.Address),
            Timezone = NormalizeTimezone(request.Timezone),
            LogoUrl = NormalizeNullable(request.LogoUrl),
            Status = CompanyStatus.Active,
            CreatedByUserId = userId,
            CreatedByUser = user,
            CreatedAt = now
        };

        company.Members.Add(new CompanyMember
        {
            CompanyId = company.Id,
            UserId = userId,
            Role = CompanyMemberRole.Owner,
            IsActive = true,
            JoinedAt = now,
            Company = company,
            User = user
        });

        await _companyRepository.AddCompanyAsync(company, cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ToCompanyResponse(company, CompanyMemberRole.Owner);
    }

    public async Task<CompanyResponse?> UpdateAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await _companyRepository.GetMembershipAsync(
            companyId,
            userId,
            cancellationToken);

        if (membership?.Company is null)
        {
            return null;
        }

        if (membership.Role != CompanyMemberRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the company owner can update company information.");
        }

        var company = membership.Company;
        company.Name = request.Name.Trim();
        company.Industry = NormalizeNullable(request.Industry);
        company.Address = NormalizeNullable(request.Address);
        company.Timezone = NormalizeTimezone(request.Timezone);
        company.LogoUrl = NormalizeNullable(request.LogoUrl);
        company.UpdatedAt = DateTimeOffset.UtcNow;

        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ToCompanyResponse(company, CompanyMemberRole.Owner);
    }

    public async Task<IReadOnlyList<CompanyMemberResponse>?> ListMembersAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _companyRepository.GetMembershipAsync(
            companyId,
            userId,
            cancellationToken);

        if (membership?.Company is null)
        {
            return null;
        }

        if (membership.Role != CompanyMemberRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the company owner can view linked monitoring accounts.");
        }

        return membership.Company.Members
            .Where(member => member.User is not null)
            .OrderBy(member => member.Role)
            .ThenBy(member => member.User!.Email)
            .Select(ToMemberResponse)
            .ToList();
    }

    public async Task<CompanyMemberResponse?> AddMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        OwnerLinkedAccountRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetOwnerMembershipAsync(
            companyId, ownerUserId, cancellationToken);

        if (owner?.Company is null) return null;

        var user = await GetValidMonitorUserAsync(
            request.Email, cancellationToken);

        var existing = owner.Company.Members
            .FirstOrDefault(member => member.UserId == user.Id);

        if (existing is not null)
        {
            if (existing.Role != CompanyMemberRole.Monitor)
            {
                throw new InvalidOperationException(
                    "This account cannot be linked as Monitor.");
            }

            if (existing.IsActive)
            {
                throw new InvalidOperationException(
                    "This account is already linked to the company.");
            }

            return await SetMonitorStatusAsync(
                ownerUserId,
                companyId,
                existing.UserId,
                true,
                ipAddress,
                cancellationToken);
        }

        var monitor = new CompanyMember
        {
            CompanyId = companyId,
            UserId = user.Id,
            JoinedAt = DateTimeOffset.UtcNow,
            Company = owner.Company,
            Role = CompanyMemberRole.Monitor,
            IsActive = true,
            User = user,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _companyRepository.AddMemberAsync(monitor, cancellationToken);
        await AddMonitorAuditAsync(
            ownerUserId,
            companyId,
            monitor,
            "MONITOR_ADDED",
            true,
            ipAddress,
            cancellationToken);

        await _companyRepository.SaveChangesAsync(cancellationToken);
        return ToMemberResponse(monitor);
    }

    public async Task<CompanyMemberResponse?> ReplaceMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        OwnerLinkedAccountRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetOwnerMembershipAsync(
            companyId, ownerUserId, cancellationToken);

        if (owner?.Company is null) return null;

        var current = owner.Company.Members.FirstOrDefault(member =>
            member.UserId == monitorUserId &&
            member.IsActive &&
            member.Role == CompanyMemberRole.Monitor);

        if (current is null) return null;

        var replacement = await GetValidMonitorUserAsync(
            request.Email, cancellationToken);

        if (replacement.Id == current.UserId)
        {
            current.User = replacement;
            return ToMemberResponse(current);
        }

        var existingReplacement = owner.Company.Members.FirstOrDefault(
            member => member.UserId == replacement.Id);

        if (existingReplacement is not null &&
            existingReplacement.Role != CompanyMemberRole.Monitor)
        {
            throw new InvalidOperationException(
                "The replacement account cannot be linked as Monitor.");
        }

        if (existingReplacement?.IsActive == true)
        {
            throw new InvalidOperationException(
                "The replacement account is already linked.");
        }

        current.IsActive = false;
        current.UpdatedAt = DateTimeOffset.UtcNow;

        var monitor = existingReplacement ?? new CompanyMember
        {
            CompanyId = companyId,
            UserId = replacement.Id,
            JoinedAt = DateTimeOffset.UtcNow,
            Company = owner.Company
        };

        monitor.Role = CompanyMemberRole.Monitor;
        monitor.IsActive = true;
        monitor.User = replacement;
        monitor.UpdatedAt = DateTimeOffset.UtcNow;

        if (existingReplacement is null)
        {
            await _companyRepository.AddMemberAsync(monitor, cancellationToken);
        }

        await AddMonitorReplacementAuditAsync(
            ownerUserId,
            companyId,
            current,
            monitor,
            ipAddress,
            cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return ToMemberResponse(monitor);
    }

    public async Task<bool> RemoveMonitorAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var result = await SetMonitorStatusAsync(
            ownerUserId,
            companyId,
            monitorUserId,
            false,
            ipAddress,
            cancellationToken);

        return result is not null;
    }

    public async Task<CompanyMemberResponse?> SetMonitorStatusAsync(
        Guid ownerUserId,
        Guid companyId,
        Guid monitorUserId,
        bool isActive,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetOwnerMembershipAsync(
            companyId, ownerUserId, cancellationToken);

        if (owner?.Company is null) return null;

        var monitor = owner.Company.Members.FirstOrDefault(member =>
            member.UserId == monitorUserId &&
            member.Role == CompanyMemberRole.Monitor);

        if (monitor is null) return null;

        if (isActive && monitor.User?.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(
                "Only an active user account can be enabled as Monitor.");
        }

        if (monitor.IsActive == isActive) return ToMemberResponse(monitor);

        monitor.IsActive = isActive;
        monitor.UpdatedAt = DateTimeOffset.UtcNow;

        await AddMonitorAuditAsync(
            ownerUserId,
            companyId,
            monitor,
            isActive ? "MONITOR_ENABLED" : "MONITOR_DISABLED",
            isActive,
            ipAddress,
            cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);
        return ToMemberResponse(monitor);
    }

    private async Task<CompanyMember?> GetOwnerMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await _companyRepository.GetMembershipAsync(
            companyId, userId, cancellationToken);

        if (membership is null) return null;

        if (membership.Role != CompanyMemberRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the company owner can manage monitoring accounts.");
        }

        return membership;
    }

    private async Task<User> GetValidMonitorUserAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(
            email.Trim().ToLowerInvariant(), cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(
                "Email must belong to an active SynTwin account.");
        }

        if (user.Role == UserRole.SuperAdmin)
        {
            throw new InvalidOperationException(
                "SuperAdmin cannot be linked as Monitor.");
        }

        return user;
    }

    private async Task<string> CreateUniqueSlugAsync(
        string companyName,
        CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlug(companyName);
        var candidate = baseSlug;
        var suffix = 2;

        while (await _companyRepository.SlugExistsAsync(
                   candidate,
                   cancellationToken: cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string CreateSlug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasDash = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasDash = false;
            }
            else if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug)
            ? $"company-{Guid.NewGuid():N}"
            : slug;
    }

    private static CompanyResponse ToCompanyResponse(
    Company company,
    CompanyMemberRole currentUserRole)
    {
        var activePlan = company.CreatedByUser?
            .Subscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan)
            .FirstOrDefault(plan => plan is not null);

        return new CompanyResponse
        {
            Id = company.Id,
            Name = company.Name,
            Slug = company.Slug,
            Industry = company.Industry,
            Address = company.Address,
            Timezone = company.Timezone,
            LogoUrl = company.LogoUrl,
            Status = company.Status.ToString(),
            CurrentUserRole = currentUserRole.ToString(),
            MemberCount = company.Members.Count(member => member.IsActive),
            SubscriptionPlan =
                activePlan?.Code.ToString() ??
                SubscriptionPlanCode.Free.ToString(),
            MaxRobots = activePlan?.MaxRobots ?? 1,
            CanView3D = activePlan?.CanView3D ?? false,
            CanSendCommand =
                activePlan?.CanSendCommand ?? false,
            CreatedAt = company.CreatedAt
        };
    }

    private static CompanyMemberResponse ToMemberResponse(CompanyMember member)
    {
        return new CompanyMemberResponse
        {
            UserId = member.UserId,
            Email = member.User?.Email ?? string.Empty,
            FullName = member.User?.FullName,
            AvatarUrl = member.User?.AvatarUrl,
            Role = member.Role.ToString(),
            IsActive = member.IsActive,
            JoinedAt = member.JoinedAt
        };
    }

    private static string NormalizeTimezone(string? timezone)
    {
        return string.IsNullOrWhiteSpace(timezone)
            ? "Asia/Ho_Chi_Minh"
            : timezone.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Task AddMonitorAuditAsync(
        Guid ownerUserId,
        Guid companyId,
        CompanyMember monitor,
        string action,
        bool isActive,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = ownerUserId,
            Action = action,
            IpAddress = NormalizeNullable(ipAddress),
            Message =
                $"Monitor '{monitor.User?.Email}' status changed to " +
                $"{(isActive ? "active" : "disabled")}.",
            RawPayloadJson = JsonSerializer.Serialize(new
            {
                companyId,
                targetUserId = monitor.UserId,
                monitorEmail = monitor.User?.Email,
                isActive
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private Task AddMonitorReplacementAuditAsync(
        Guid ownerUserId,
        Guid companyId,
        CompanyMember previousMonitor,
        CompanyMember replacementMonitor,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = ownerUserId,
            Action = "MONITOR_REPLACED",
            IpAddress = NormalizeNullable(ipAddress),
            Message =
                $"Monitor '{previousMonitor.User?.Email}' was replaced by " +
                $"'{replacementMonitor.User?.Email}'.",
            RawPayloadJson = JsonSerializer.Serialize(new
            {
                companyId,
                previousMonitorUserId = previousMonitor.UserId,
                previousMonitorEmail = previousMonitor.User?.Email,
                replacementMonitorUserId = replacementMonitor.UserId,
                replacementMonitorEmail = replacementMonitor.User?.Email
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
