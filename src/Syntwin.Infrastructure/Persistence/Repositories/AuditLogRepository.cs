using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Infrastructure.Persistence;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly SyntwinDbContext _dbContext;

    public AuditLogRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }
}