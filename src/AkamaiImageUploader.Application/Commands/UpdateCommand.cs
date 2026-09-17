using System.Text.RegularExpressions;
using Akamai.NetStorage;

namespace AkamaiImageUploader;

public sealed partial class UpdateCommand
{
    private readonly INetStorageClient _netStorage;

    public UpdateCommand(INetStorageClient netStorage)
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
            Console.Error.WriteLine("Es werden zwei Argumente benötigt: Bildpfad und alter Dateiname.");
            PrintUsage();
            return 1;
        }

        var imagePath = Path.GetFullPath(args[0]);
        var oldFileName = args[1];

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
            remoteFileName = BuildRemoteFileName(oldFileName, imagePath);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return await ExecuteCoreAsync(
            oldFileName,
            remoteFileName,
            ct => _netStorage.UploadAsync(imagePath, remoteFileName, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Programmatischer Zugang für Aufrufer, die das Bild bereits als Byte-Array vorliegen haben
    /// (z. B. bei Nutzung dieser Application-Schicht als Library) statt es von der Festplatte zu lesen.
    /// </summary>
    public async Task<int> ExecuteAsync(
        byte[] imageContent,
        string fileNameHint,
        string oldFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameHint);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldFileName);

        var remoteFileName = BuildRemoteFileName(oldFileName, fileNameHint);
        var contentType = ContentType.Guess(fileNameHint);

        return await ExecuteCoreAsync(
            oldFileName,
            remoteFileName,
            ct => UploadBytesAsync(imageContent, remoteFileName, contentType, ct),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task UploadBytesAsync(
        byte[] content,
        string remoteFileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content, writable: false);
        await _netStorage.UploadAsync(stream, remoteFileName, contentType, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<int> ExecuteCoreAsync(
        string oldFileName,
        string remoteFileName,
        Func<CancellationToken, Task> uploadAsync,
        CancellationToken cancellationToken)
    {
        _netStorage.ValidateConfiguration();

        Console.WriteLine("Stelle Verbindung zu Akamai NetStorage her ...");
        await _netStorage.ValidateConnectionAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Login erfolgreich.");

        Console.WriteLine($"Lösche altes Bild '{oldFileName}' ...");
        await _netStorage.DeleteAsync(oldFileName, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Altes Bild gelöscht.");

        Console.WriteLine($"Lade neues Bild hoch als '{remoteFileName}' ...");
        await uploadAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Update erfolgreich.");
        return 0;
    }

    private static string BuildRemoteFileName(string oldFileName, string newImageSourcePath)
    {
        var designation = DeriveDesignation(oldFileName);
        return UploadCommand.AppendTimestamp(UploadCommand.ResolveRemoteFileName(designation, newImageSourcePath));
    }

    /// <summary>
    /// Leitet aus dem alten Dateinamen die ursprüngliche Bezeichnung ab, indem ein zuvor
    /// automatisch angehängter Zeitstempel (siehe <see cref="UploadCommand.AppendTimestamp"/>)
    /// entfernt wird. So erhält das aktualisierte Bild wieder denselben Basisnamen, aber einen
    /// neuen Zeitstempel und die Dateiendung des neuen Bildes.
    /// </summary>
    internal static string DeriveDesignation(string oldFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(oldFileName);
        var match = TimestampSuffixRegex().Match(baseName);
        return match.Success ? baseName[..match.Index] : baseName;
    }

    [GeneratedRegex(@"_\d{14}$")]
    private static partial Regex TimestampSuffixRegex();

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Akamai Image Uploader — Update

            Aufruf:
              AkamaiImageUploader update <bildpfad> <alter-dateiname>

            Löscht das alte Bild und lädt das neue an dessen Stelle hoch.
            Der Basisname wird vom alten Dateinamen übernommen, dem Dateinamen
            wird automatisch ein neuer Zeitstempel angehängt.
            """);
    }
}
