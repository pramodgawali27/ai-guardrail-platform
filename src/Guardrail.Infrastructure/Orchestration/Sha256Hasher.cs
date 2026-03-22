using System.Security.Cryptography;
using System.Text;

namespace Guardrail.Infrastructure.Orchestration;

internal static class Sha256Hasher
{
    public static string Hash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
