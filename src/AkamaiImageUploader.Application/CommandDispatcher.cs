using Microsoft.Extensions.DependencyInjection;

namespace AkamaiImageUploader;

public static class CommandDispatcher
{
    public static async Task<int> ExecuteAsync(
        IServiceProvider services,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var verb = args[0];
        var rest = args[1..];

        try
        {
            return verb.ToLowerInvariant() switch
            {
                "list" or "ls" => await services.GetRequiredService<ListCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                "delete" or "rm" => await services.GetRequiredService<DeleteCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                "download" or "get" => await services.GetRequiredService<DownloadCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                "upload" => await services.GetRequiredService<UploadCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                "update" => await services.GetRequiredService<UpdateCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                "purge" => await services.GetRequiredService<PurgeCommand>()
                    .ExecuteAsync(rest, cancellationToken).ConfigureAwait(false),
                _ => await services.GetRequiredService<UploadCommand>()
                    .ExecuteAsync(args, cancellationToken).ConfigureAwait(false)
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or ArgumentException)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Akamai Image Uploader

            Aufruf:
              AkamaiImageUploader <bildpfad> <bezeichnung>
              AkamaiImageUploader upload <bildpfad> <bezeichnung>
              AkamaiImageUploader update <bildpfad> <alter-dateiname>
              AkamaiImageUploader list [unterordner]
              AkamaiImageUploader delete <dateiname>
              AkamaiImageUploader download <dateiname> [zielpfad]
              AkamaiImageUploader purge <dateiname-oder-url> [...]

            Argumente:
              bildpfad        Pfad zur lokalen Bilddatei
              bezeichnung     Freier Name in NetStorage. Fehlt die Dateiendung,
                              wird die Endung der Quelldatei übernommen.
              alter-dateiname Name des zu ersetzenden Bildes in NetStorage.
              dateiname       Name der Datei relativ zu Akamai:NetStorage:RemotePath.

            Zugangsdaten stehen in appsettings.json (Abschnitt Akamai).
            Hochgeladenen Dateinamen wird automatisch ein Zeitstempel angehängt.
            'update' löscht das alte Bild und lädt das neue an dessen Stelle hoch.
            Der CDN-Cache wird nicht automatisch invalidiert. Damit ein erneuter
            Upload unter gleichem Namen nicht das alte Bild ausliefert, muss die
            Invalidierung separat über 'purge <dateiname-oder-url>' ausgelöst werden.
            """);
    }
}
