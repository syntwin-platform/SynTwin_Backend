using System.Security.Cryptography;
using System.Text;

namespace Syntwin.Application.FactoryRuns.Models;

public static class FactoryRunOperationIdentity
{
    public static Guid CreateCommandId(
        Guid factoryRunId,
        Guid targetId,
        string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var identity = $"{factoryRunId:N}:{targetId:N}:{operation.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var bytes = hash.AsSpan(0, 16).ToArray();

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
