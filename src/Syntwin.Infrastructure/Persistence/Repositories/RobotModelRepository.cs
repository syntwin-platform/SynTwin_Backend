using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotModelRepository : IRobotModelRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotModelRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RobotModel>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RobotModels
            .AsNoTracking()
            .Where(model => model.IsActive)
            .OrderBy(model => model.Vendor)
            .ThenBy(model => model.ModelCode)
            .ToListAsync(cancellationToken);
    }

    public Task<RobotModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotModels
            .AsNoTracking()
            .FirstOrDefaultAsync(model => model.Id == id, cancellationToken);
    }
}