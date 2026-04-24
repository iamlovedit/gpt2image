using System.Security.Cryptography;

namespace ImageRelay.Api.Services;

public record GeneratedApiKey(string Plaintext, string Prefix, string Hash);

public class ApiKeyGenerator
{
    private const string KeyPrefix = "sk-";

    public GeneratedApiKey Generate()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var body = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var plaintext = KeyPrefix + body;
        var prefix = plaintext[..Math.Min(plaintext.Length, 8)];
        var hash = HashKey(plaintext);
        return new GeneratedApiKey(plaintext, prefix, hash);
    }

    public string HashKey(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
