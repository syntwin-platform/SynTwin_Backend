namespace Syntwin.Application.FactoryRuns.Models;

public sealed record FactoryRunArmBarrierRegistration(
    int ArmedCount,
    int ExpectedParticipantCount,
    bool ShouldSeal,
    FactoryRunArmBarrierReadyState? Ready);

public sealed record FactoryRunArmBarrierReadyState(
    DateTimeOffset ScheduledStartAtUtc,
    int ExpectedParticipantCount,
    IReadOnlyList<int> StepDurationsMs);
