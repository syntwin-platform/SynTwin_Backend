using Syntwin.Application.LuaParsing.Models;

namespace Syntwin.Application.LuaParsing.Interfaces;

public interface ILuaProgramParser
{
    LuaParseResult Parse(string luaContent, string? fileName = null);
}