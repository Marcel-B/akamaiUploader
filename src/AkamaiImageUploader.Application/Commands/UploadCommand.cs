using Akamai.NetStorage;
using AkamaiImageUploader.Purge;

namespace AkamaiImageUploader;

public sealed class UploadCommand
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", ".avif"
    ];

    private readonly INetStorageClient _netStorage;
    private readonly CachePurgeClient _cachePurge;

    public UploadCommand(INetStorageClient netStorage, CachePurgeClient cachePurge)
    {
        _netStorage = netStorage;
        _cachePurge = cachePurge;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Es werden zwei Argumente benötigt: Bildpfad und Bezeichnung.");
            PrintUsage();
            return 1;
        }

        var imagePath = Path.GetFullPath(args[0]);
        var designation = args[1];

        if (Directory.Exists(imagePath))
        {
            Console.Error.WriteLine("Bitte den Pfad zur Bilddatei angeben, nicht nur den Ordner.");
            return 1;
        }

        if (!File.Exists(imagePath))
        {
            Console.Error.WriteLine($"Bilddatei nicht gefunden: {imagePath}");
            return 1;
        }

        string remoteFileName;
        try
        {
            remoteFileName = ResolveRemoteFileName(designation, imagePath);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var extension = Path.GetExtension(imagePath);
        if (!ImageExtensions.Contains(extension.ToLowerInvariant()))
        {
            Console.WriteLine($"Hinweis: '{extension}' ist keine typische Bildendung. Der Upload wird trotzdem versucht.");
        }

        _netStorage.ValidateConfiguration();

        Console.WriteLine("Stelle Verbindung zu Akamai NetStorage her ...");
        await _netStorage.ValidateConnectionAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Login erfolgreich.");

        Console.WriteLine($"Lade Bild hoch als '{remoteFileName}' ...");
        await _netStorage.UploadAsync(imagePath, remoteFileName, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Upload erfolgreich.");

        await InvalidateCacheAsync(remoteFileName, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Fertig.");
        return 0;
    }

    private async Task InvalidateCacheAsync(string remoteFileName, CancellationToken cancellationToken)
    {
        if (!_cachePurge.IsConfigured)
        {
            Console.WriteLine(
                "Cache-Invalidierung übersprungen: EdgeGrid-Zugangsdaten fehlen oder CachePurge ist deaktiviert.");
            Console.WriteLine(
                "Ohne Fast Purge kann der CDN-Cache bei gleichem Namen noch das alte Bild ausliefern.");
            return;
        }

        var publicUrl = _cachePurge.BuildPublicUrl(remoteFileName);
        Console.WriteLine($"Invalidiere CDN-Cache für {publicUrl} ...");
        var result = await _cachePurge.InvalidateUrlAsync(publicUrl, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Cache-Invalidierung ausgelöst{PurgeCommand.Format(result)}.");
    }

    internal static string ResolveRemoteFileName(string designation, string sourcePath)
    {
        var name = Path.GetFileName(designation.Replace('\\', '/').Trim());
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            throw new ArgumentException("Die Bezeichnung darf nicht leer sein und keinen Pfad enthalten.");
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Die Bezeichnung enthält ungültige Zeichen für einen Dateinamen.");
        }

        if (string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            name += Path.GetExtension(sourcePath);
        }

        return name;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Akamai Image Uploader — Upload

            Aufruf:
              AkamaiImageUploader <bildpfad> <bezeichnung>
              AkamaiImageUploader upload <bildpfad> <bezeichnung>
            """);
    }
}
