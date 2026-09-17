using Akamai.NetStorage;

namespace AkamaiImageUploader;

public sealed class UploadCommand
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", ".avif"
    ];

    private readonly INetStorageClient _netStorage;

    public UploadCommand(INetStorageClient netStorage)
    {
        _netStorage = netStorage;
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
            remoteFileName = AppendTimestamp(ResolveRemoteFileName(designation, imagePath));
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
        Console.WriteLine("Fertig.");
        return 0;
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

    internal static string AppendTimestamp(string fileName, DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var stamp = (timestamp ?? DateTimeOffset.UtcNow).ToString("yyyyMMddHHmmss");
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return $"{baseName}_{stamp}{extension}";
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

            Hinweis: Dem Dateinamen wird automatisch ein Zeitstempel angehängt.
            Der CDN-Cache wird nicht automatisch invalidiert.
            Dafür separat 'AkamaiImageUploader purge <dateiname-oder-url>' ausführen.
            """);
    }
}
