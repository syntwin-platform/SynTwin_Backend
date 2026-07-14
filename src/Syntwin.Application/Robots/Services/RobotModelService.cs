using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Application.Robots.Services;

public sealed class RobotModelService : IRobotModelService
{
    private readonly IRobotModelRepository _robotModelRepository;

    public RobotModelService(IRobotModelRepository robotModelRepository)
    {
        _robotModelRepository = robotModelRepository;
    }

    public async Task<IReadOnlyList<RobotModelResponse>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var models = await _robotModelRepository.ListActiveAsync(cancellationToken);

        return models
            .Select(ToResponse)
            .ToList();
    }

    private static RobotModelResponse ToResponse(RobotModel model)
    {
        return new RobotModelResponse
        {
            Id = model.Id,
            Vendor = model.Vendor,
            ModelCode = model.ModelCode,
            DisplayName = model.DisplayName,
            Dof = model.Dof,
            Description = model.Description,
            UrdfPath = model.UrdfPath,
            MeshRootPath = model.MeshRootPath,
            DefaultTcpFrame = model.DefaultTcpFrame,
            JointNamesJson = model.JointNamesJson,
            JointLimitsJson = model.JointLimitsJson,
            IsActive = model.IsActive
        };
    }
}