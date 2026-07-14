using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class FactoryRunPreparedProgram
{
    public Guid RobotId { get; set; }

    public RobotProgramResponse? Program { get; set; }

    public string? Error { get; set; }

    public bool IsSuccess => Program is not null && string.IsNullOrWhiteSpace(Error);
}
