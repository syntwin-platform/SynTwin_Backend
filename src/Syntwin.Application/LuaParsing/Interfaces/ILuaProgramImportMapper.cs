using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Models;

namespace Syntwin.Application.LuaParsing.Interfaces;

public interface ILuaProgramImportMapper
{
    LuaParsePreviewResponse ToPreviewResponse(LuaParseResult parseResult);
}