namespace AkamaiImageUploader.Configuration;

public sealed class AkamaiOptions
{
    public const string SectionName = "Akamai";

    public NetStorageOptions NetStorage { get; set; } = new();

    public CachePurgeOptions CachePurge { get; set; } = new();
}

public sealed class NetStorageOptions
{
    /// <summary>
    /// Hostname der Usage API, z. B. example-nsu.akamaihd.net
    /// oder nur der Domain-Prefix (dann wird -nsu.akamaihd.net ergänzt).
    /// </summary>
    public string Host { get; set; } = "";

    public string CpCode { get; set; } = "";

    public string UploadAccountId { get; set; } = "";

    public string Key { get; set; } = "";

    /// <summary>
    /// Unterordner innerhalb des CP-Code-Roots, z. B. images.
    /// </summary>
    public string RemotePath { get; set; } = "images";

    public string ResolvedHost()
    {
        var host = Host.Trim();
        host = host.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        if (string.IsNullOrWhiteSpace(host))
        {
            return host;
        }

        return host.Contains('.', StringComparison.Ordinal) ? host : $"{host}-nsu.akamaihd.net";
    }
}

public sealed class CachePurgeOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// EdgeGrid-API-Host, z. B. akab-xxxxx.luna.akamaiapis.net
    /// </summary>
    public string Host { get; set; } = "";

    public string ClientToken { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string AccessToken { get; set; } = "";

    /// <summary>
    /// production oder staging
    /// </summary>
    public string Network { get; set; } = "production";

    /// <summary>
    /// Öffentliche CDN-Basis-URL, unter der das Bild ausgeliefert wird.
    /// Die Invalidierung trifft {PublicBaseUrl}/{Dateiname}.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

    public string ResolvedHost()
    {
        return Host.Trim()
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
    }

    public string ResolvedNetwork()
    {
        return string.Equals(Network, "staging", StringComparison.OrdinalIgnoreCase)
            ? "staging"
            : "production";
    }

    public bool HasCredentials()
    {
        return !string.IsNullOrWhiteSpace(ResolvedHost())
            && !string.IsNullOrWhiteSpace(ClientToken)
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && !string.IsNullOrWhiteSpace(AccessToken)
            && !string.IsNullOrWhiteSpace(PublicBaseUrl);
    }
}
