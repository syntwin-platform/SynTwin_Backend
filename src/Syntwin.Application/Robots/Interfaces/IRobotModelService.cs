using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotModelService
{
    Task<IReadOnlyList<RobotModelResponse>> ListActiveAsync(
        CancellationToken cancellationToken = default);
}