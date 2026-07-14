using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class FactoryRunProgramPreparationItem
{
    public Guid RobotId { get; set; }

    public CreateRobotProgramRequest CreateProgramRequest { get; set; } = new();
}