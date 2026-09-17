using System.Globalization;
using System.Xml.Linq;

namespace Akamai.NetStorage;

internal static class DirResponseParser
{
    public static NetStorageListResult Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw new NetStorageException("Die Dateiliste von NetStorage konnte nicht gelesen werden.", innerException: ex);
        }

        var stat = document.Element("stat")
            ?? throw new NetStorageException("Ungültige dir-Antwort: Element 'stat' fehlt.");

        var directory = (string?)stat.Attribute("directory") ?? "";
        var entries = new List<NetStorageEntry>();

        foreach (var file in stat.Elements("file"))
        {
            var name = ReadName(file, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var type = ParseType((string?)file.Attribute("type"));
            var mtime = ReadInt64(file, "mtime");
            DateTimeOffset? modifiedAt = mtime is long unix
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : null;

            entries.Add(new NetStorageEntry(
                Name: name,
                Type: type,
                Size: type == NetStorageEntryType.Directory ? ReadInt64(file, "bytes") : ReadInt64(file, "size"),
                Md5: (string?)file.Attribute("md5"),
                ModifiedAt: modifiedAt,
                IsImplicitDirectory: string.Equals((string?)file.Attribute("implicit"), "true", StringComparison.OrdinalIgnoreCase),
                SymlinkTarget: ReadName(file, "target"),
                DirectoryFileCount: type == NetStorageEntryType.Directory ? (int?)ReadInt64(file, "files") : null));
        }

        var resumeStart = (string?)stat.Element("resume")?.Attribute("start");
        return new NetStorageListResult(directory, entries, resumeStart);
    }

    private static NetStorageEntryType ParseType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "file" => NetStorageEntryType.File,
            "dir" => NetStorageEntryType.Directory,
            "symlink" => NetStorageEntryType.Symlink,
            _ => NetStorageEntryType.Unknown
        };
    }

    private static string? ReadName(XElement element, string attribute)
    {
        var value = (string?)element.Attribute(attribute);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        var encoded = (string?)element.Attribute($"{attribute}_base64");
        if (string.IsNullOrEmpty(encoded))
        {
            return value;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return encoded;
        }
    }

    private static long? ReadInt64(XElement element, string attribute)
    {
        var value = (string?)element.Attribute(attribute);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
