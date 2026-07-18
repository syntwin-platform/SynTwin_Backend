using System.ComponentModel.DataAnnotations;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class CreateFactoryRunRequest
{
    public Guid? ClientRequestId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    public FactoryCoordinationMode CoordinationMode { get; set; } =
        FactoryCoordinationMode.Synchronized;

    public FactoryFailurePolicy FailurePolicy { get; set; } =
        FactoryFailurePolicy.IsolateTarget;

    [MaxLength(100)]
    public string? ProgramName { get; set; }

    [MaxLength(260)]
    public string? LuaFileName { get; set; }

    public string? LuaContent { get; set; }

    public List<Guid> RobotIds { get; set; } = new();

    public List<CreateFactoryRunProgramRequest> Programs { get; set; } = new();

    public List<CreateFactoryRunTargetRequest> Targets { get; set; } = new();
}
