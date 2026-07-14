using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotRepository
{
    Task<IReadOnlyList<Robot>> ListAccessibleByUserIdAsync(
        Guid userId,
        Guid? companyId = null,
        CancellationToken cancellationToken = default);

    Task<Robot?> GetByIdAsync(Guid robotId, CancellationToken cancellationToken = default);
    Task<Robot?> GetByIdReadOnlyAsync(
    Guid robotId,
    CancellationToken cancellationToken = default);

    Task<RobotSceneBinding?> GetSceneBindingByRobotIdReadOnlyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveOwnedByUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Robot robot, CancellationToken cancellationToken = default);

    Task AddSceneBindingAsync(
    RobotSceneBinding sceneBinding,
    CancellationToken cancellationToken = default);

    Task<bool> UpdateSceneBindingByRobotIdAsync(
        RobotSceneBinding sceneBinding,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Robot>> ListByStatusAsync(
    RobotStatus status,
    CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Robot>> ListByIdsAsync(
    IReadOnlyCollection<Guid> robotIds,
    CancellationToken cancellationToken = default);


}
