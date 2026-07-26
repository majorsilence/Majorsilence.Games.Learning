using System.Security.Cryptography;
using System.Text;

namespace Majorsilence.Games.Server;

/// <summary>
/// Device bearer tokens: 32 random bytes, base64url-encoded, handed to the
/// client once at registration. The server only ever stores/compares the
/// SHA-256 hash - a stolen database row can't be turned back into a usable
/// token.
/// </summary>
public static class TokenService
{
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I - avoids transcription mistakes

    /// <summary>An 8-character link code, ~2.8e11 possibilities - paired with a short TTL and single use, safe against brute force even unauthenticated.</summary>
    public static string GenerateLinkCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }
}
