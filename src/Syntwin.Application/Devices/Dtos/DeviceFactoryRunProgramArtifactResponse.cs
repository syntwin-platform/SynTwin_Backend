using System.Text.Json;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunProgramArtifactStepResponse
{
    public int OrderIndex { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }
}

public sealed class DeviceFactoryRunProgramArtifactResponse
{
    public Guid FactoryRunId { get; set; }

    public Guid TargetId { get; set; }

    public Guid FactoryRunProgramId { get; set; }

    public int ContractVersion { get; set; } = 1;

    public string CompiledProgramHash { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public IReadOnlyList<DeviceFactoryRunProgramArtifactStepResponse> Steps { get; set; } =
        Array.Empty<DeviceFactoryRunProgramArtifactStepResponse>();
}

public sealed class DeviceFactoryRunProgramArtifactResult
{
    public bool IsAuthenticated { get; set; }

    public bool IsDisabled { get; set; }

    public DeviceFactoryRunProgramArtifactResponse? Artifact { get; set; }
}
