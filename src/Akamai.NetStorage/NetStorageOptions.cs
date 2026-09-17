namespace Akamai.NetStorage;

public sealed class NetStorageOptions
{
    public const string SectionName = "Akamai:NetStorage";

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
    /// Alle Dateioperationen gelten relativ zu diesem Pfad.
    /// </summary>
    public string RemotePath { get; set; } = "";

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

    internal IReadOnlyList<string> MissingSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ResolvedHost()))
        {
            missing.Add("Host");
        }

        if (string.IsNullOrWhiteSpace(CpCode))
        {
            missing.Add("CpCode");
        }

        if (string.IsNullOrWhiteSpace(UploadAccountId))
        {
            missing.Add("UploadAccountId");
        }

        if (string.IsNullOrWhiteSpace(Key))
        {
            missing.Add("Key");
        }

        return missing;
    }
}
