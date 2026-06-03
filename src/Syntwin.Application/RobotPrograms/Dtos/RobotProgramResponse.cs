namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class RobotProgramResponse
{
    public Guid Id { get; set; }

    public Guid RobotId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyList<RobotProgramStepResponse> Steps { get; set; } = [];
}