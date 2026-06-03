using Syntwin.Domain.Entities;

namespace Syntwin.Application.AuditLogs.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}