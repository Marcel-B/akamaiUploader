namespace Akamai.NetStorage;

internal static class NetStoragePath
{
    public static string Combine(string cpCode, string remotePath, params string[] extraSegments)
    {
        var segments = new List<string> { cpCode.Trim().Trim('/') };
        segments.AddRange(Split(remotePath));
        foreach (var extra in extraSegments)
        {
            segments.AddRange(Split(extra));
        }

        if (segments.Count == 0 || string.IsNullOrWhiteSpace(segments[0]))
        {
            throw new ArgumentException("CP-Code darf nicht leer sein.", nameof(cpCode));
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException("Relative Pfadangaben mit '.' oder '..' sind nicht erlaubt.");
            }
        }

        return "/" + string.Join("/", segments.Select(Uri.EscapeDataString));
    }

    public static IEnumerable<string> Split(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/'))
        {
            throw new ArgumentException("Der Pfad muss relativ sein und darf nicht mit '/' beginnen.", nameof(path));
        }

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string RequireFileName(string remoteFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteFileName);
        var segments = Split(remoteFileName.Replace('\\', '/').Trim().Trim('/')).ToList();
        if (segments.Count == 0)
        {
            throw new ArgumentException("Der Dateiname darf nicht leer sein.", nameof(remoteFileName));
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException("Relative Pfadangaben mit '.' oder '..' sind nicht erlaubt.", nameof(remoteFileName));
            }
        }

        return string.Join("/", segments);
    }
}
