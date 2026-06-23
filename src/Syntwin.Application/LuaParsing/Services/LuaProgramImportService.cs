using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.Robots.Interfaces;

namespace Syntwin.Application.LuaParsing.Services;

public sealed class LuaProgramImportService : ILuaProgramImportService
{
    private const int MaxLuaContentLength = 1024 * 1024;

    private readonly IRobotRepository _robotRepository;
    private readonly IRobotAccessService _robotAccessService;
    private readonly IRobotProgramService _robotProgramService;
    private readonly ILuaProgramParser _luaProgramParser;
    private readonly ILuaProgramImportMapper _luaProgramImportMapper;

    public LuaProgramImportService(
        IRobotRepository robotRepository,
        IRobotAccessService robotAccessService,
        IRobotProgramService robotProgramService,
        ILuaProgramParser luaProgramParser,
        ILuaProgramImportMapper luaProgramImportMapper)
    {
        _robotRepository = robotRepository;
        _robotAccessService = robotAccessService;
        _robotProgramService = robotProgramService;
        _luaProgramParser = luaProgramParser;
        _luaProgramImportMapper = luaProgramImportMapper;
    }

    public async Task<LuaParsePreviewResponse?> PreviewAsync(
        Guid userId,
        Guid robotId,
        LuaParseRequest request,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null)
        {
            return null;
        }

        var role = await _robotAccessService.GetCompanyRoleAsync(
            userId,
            robot.CompanyId,
            cancellationToken);

        if (!role.HasValue)
        {
            return null;
        }

        ValidateRequest(request);

        var parseResult = _luaProgramParser.Parse(
            request.LuaContent,
            request.FileName);

        return _luaProgramImportMapper.ToPreviewResponse(parseResult);
    }

    public async Task<RobotProgramResponse?> ImportAsync(
        Guid userId,
        Guid robotId,
        LuaParseRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(
            userId,
            robotId,
            request,
            cancellationToken);

        if (preview is null)
        {
            return null;
        }

        if (preview.CreateProgramRequest is null)
        {
            var firstError = preview.Diagnostics
                .FirstOrDefault(diagnostic => diagnostic.Severity == "error");

            throw new InvalidOperationException(
                firstError?.Message ?? "The LUA file does not contain any valid robot program steps.");
        }

        return await _robotProgramService.CreateAsync(
            userId,
            robotId,
            preview.CreateProgramRequest,
            ipAddress,
            cancellationToken);
    }

    private static void ValidateRequest(LuaParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LuaContent))
        {
            throw new InvalidOperationException("The LUA content is required.");
        }

        if (request.LuaContent.Length > MaxLuaContentLength)
        {
            throw new InvalidOperationException("The LUA file must not exceed 1 MB.");
        }

        if (!string.IsNullOrWhiteSpace(request.FileName) &&
            !request.FileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .lua files are supported.");
        }
    }
}