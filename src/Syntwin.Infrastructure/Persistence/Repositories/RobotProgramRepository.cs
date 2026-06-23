using Microsoft.EntityFrameworkCore;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotProgramRepository : IRobotProgramRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotProgramRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RobotProgram>> ListByRobotIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RobotPrograms
    .AsNoTracking()
    .Include(program => program.Steps)
    .Where(program =>
        program.RobotId == robotId &&
        program.Status != RobotProgramStatus.Archived)
    .OrderByDescending(program => program.UpdatedAt ?? program.CreatedAt)
    .ToListAsync(cancellationToken);
    }

    public Task<RobotProgram?> GetByIdForRobotAsync(
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotPrograms
            .Include(program => program.Steps)
            .FirstOrDefaultAsync(
                program => program.Id == programId && program.RobotId == robotId,
                cancellationToken);
    }

    public async Task AddAsync(
        RobotProgram program,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotPrograms.AddAsync(program, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}