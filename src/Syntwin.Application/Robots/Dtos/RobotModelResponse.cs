namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotModelResponse
{
    public Guid Id { get; set; }

    public string Vendor { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Dof { get; set; }

    public string? Description { get; set; }

    public string? UrdfPath { get; set; }

    public string? MeshRootPath { get; set; }

    public string? DefaultTcpFrame { get; set; }

    public string? JointNamesJson { get; set; }

    public string? JointLimitsJson { get; set; }

    public bool IsActive { get; set; }
}