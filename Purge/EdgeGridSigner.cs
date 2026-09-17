using System.Security.Cryptography;
using System.Text;

namespace AkamaiImageUploader.Purge;

/// <summary>
/// EdgeGrid EG1-HMAC-SHA256 für Akamai OPEN APIs (Fast Purge).
/// </summary>
public static class EdgeGridSigner
{
    private const int MaxBodyBytes = 131_072;

    public static string CreateAuthorizationHeader(
        string method,
        Uri requestUri,
        byte[]? body,
        string clientToken,
        string clientSecret,
        string accessToken,
        DateTimeOffset? utcNow = null,
        string? nonce = null)
    {
        var timestamp = (utcNow ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("yyyyMMddTHH:mm:ss+0000");
        nonce ??= Guid.NewGuid().ToString();

        var authHeader =
            $"EG1-HMAC-SHA256 client_token={clientToken};access_token={accessToken};timestamp={timestamp};nonce={nonce};";

        var contentHash = "";
        if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) && body is { Length: > 0 })
        {
            var slice = body.Length > MaxBodyBytes ? body.AsSpan(0, MaxBodyBytes) : body.AsSpan();
            contentHash = Convert.ToBase64String(SHA256.HashData(slice));
        }

        var relativeUrl = requestUri.PathAndQuery;
        var dataToSign = string.Join('\t',
            method.ToUpperInvariant(),
            requestUri.Scheme,
            requestUri.Authority,
            relativeUrl,
            "",
            contentHash,
            authHeader);

        var signingKey = Base64HmacSha256(timestamp, clientSecret);
        var signature = Base64HmacSha256(dataToSign, signingKey);
        return authHeader + "signature=" + signature;
    }

    private static string Base64HmacSha256(string data, string key)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
