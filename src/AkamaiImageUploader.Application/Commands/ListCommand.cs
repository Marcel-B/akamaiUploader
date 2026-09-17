using Akamai.NetStorage;

namespace AkamaiImageUploader;

public sealed class ListCommand
{
    private readonly INetStorageClient _netStorage;

    public ListCommand(INetStorageClient netStorage)
    {
        _netStorage = netStorage;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length > 0 && IsHelp(args[0]))
        {
            Console.WriteLine("Aufruf: AkamaiImageUploader list [unterordner]");
            return 0;
        }

        var relativeDirectory = args.Length > 0 ? args[0] : null;
        var result = await _netStorage.ListAsync(relativeDirectory, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Verzeichnis: {result.Directory}");
        if (result.Entries.Count == 0)
        {
            Console.WriteLine("Keine Einträge.");
            return 0;
        }

        foreach (var entry in result.Entries)
        {
            var kind = entry.Type switch
            {
                NetStorageEntryType.Directory => "dir ",
                NetStorageEntryType.Symlink => "link",
                NetStorageEntryType.File => "file",
                _ => "    "
            };
            var size = entry.Size?.ToString() ?? "-";
            var modified = entry.ModifiedAt?.UtcDateTime.ToString("u") ?? "-";
            Console.WriteLine($"{kind}\t{size,12}\t{modified}\t{entry.Name}");
        }

        if (!string.IsNullOrEmpty(result.ResumeStart))
        {
            Console.WriteLine($"Weitere Einträge vorhanden. Resume-Start: {result.ResumeStart}");
        }

        return 0;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }
}
