using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.LuaParsing.Interfaces;

public interface ILuaProgramImportService
{
    Task<LuaParsePreviewResponse?> PreviewAsync(
        Guid userId,
        Guid robotId,
        LuaParseRequest request,
        CancellationToken cancellationToken = default);

    Task<RobotProgramResponse?> ImportAsync(
        Guid userId,
        Guid robotId,
        LuaParseRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}