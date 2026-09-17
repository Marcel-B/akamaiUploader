using Akamai.NetStorage;

namespace AkamaiImageUploader;

public sealed class DownloadCommand
{
    private readonly INetStorageClient _netStorage;

    public DownloadCommand(INetStorageClient netStorage)
    {
        _netStorage = netStorage;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Aufruf: AkamaiImageUploader download <dateiname> [zielpfad]");
            return args.Length == 0 ? 1 : 0;
        }

        var remoteFileName = args[0];
        var destination = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.GetFullPath(Path.GetFileName(remoteFileName.Replace('\\', '/')));

        Console.WriteLine($"Lade '{remoteFileName}' nach '{destination}' ...");
        await _netStorage.DownloadToFileAsync(remoteFileName, destination, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Download abgeschlossen.");
        return 0;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }
}
