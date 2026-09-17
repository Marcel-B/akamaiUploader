using System.Security.Cryptography;
using System.Text;

namespace AkamaiImageUploader.NetStorage;

/// <summary>
/// HMAC-SHA256-Signatur für die NetStorage Usage API (Version 5).
/// Es gibt kein Session-Login: jeder Request trägt eigene Auth-Header.
/// </summary>
public static class NetStorageSigner
{
    public static (string AuthData, string AuthSign) Sign(
        string requestPath,
        string actionHeader,
        string uploadAccountId,
        string key,
        long epochSeconds,
        int uniqueId)
    {
        var authData = $"5, 0.0.0.0, 0.0.0.0, {epochSeconds}, {uniqueId}, {uploadAccountId}";
        var signString = $"{authData}{requestPath}\nx-akamai-acs-action:{actionHeader}\n";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(signString));

        return (authData, Convert.ToBase64String(signature));
    }

    public static (string AuthData, string AuthSign) Sign(
        string requestPath,
        string actionHeader,
        string uploadAccountId,
        string key)
    {
        var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var uniqueId = RandomNumberGenerator.GetInt32(0, int.MaxValue);
        return Sign(requestPath, actionHeader, uploadAccountId, key, epoch, uniqueId);
    }
}
