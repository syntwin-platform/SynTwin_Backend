using System.ComponentModel.DataAnnotations;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class CreateFactoryRunRequest
{
    [Required]
    public Guid CompanyId { get; set; }

    public FactoryCoordinationMode CoordinationMode { get; set; } =
    FactoryCoordinationMode.Synchronized;

public FactoryFailurePolicy FailurePolicy { get; set; } =
    FactoryFailurePolicy.IsolateTarget;

    [Required]
    [MaxLength(100)]
    public string ProgramName { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string LuaFileName { get; set; } = string.Empty;

    [Required]
    public string LuaContent { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<Guid> RobotIds { get; set; } = new();
}