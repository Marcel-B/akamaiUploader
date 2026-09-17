using Akamai.NetStorage;

namespace AkamaiImageUploader;

public sealed class DeleteCommand
{
    private readonly INetStorageClient _netStorage;

    public DeleteCommand(INetStorageClient netStorage)
    {
        _netStorage = netStorage;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Aufruf: AkamaiImageUploader delete <dateiname>");
            return args.Length == 0 ? 1 : 0;
        }

        var remoteFileName = args[0];
        Console.WriteLine($"Lösche '{remoteFileName}' ...");
        await _netStorage.DeleteAsync(remoteFileName, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Datei gelöscht.");
        return 0;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }
}
