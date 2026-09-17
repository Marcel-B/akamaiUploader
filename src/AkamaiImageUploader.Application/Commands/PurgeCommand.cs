using AkamaiImageUploader.Purge;

namespace AkamaiImageUploader;

public sealed class PurgeCommand
{
    private readonly CachePurgeClient _cachePurge;

    public PurgeCommand(CachePurgeClient cachePurge)
    {
        _cachePurge = cachePurge;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        _cachePurge.EnsureConfigured();

        foreach (var target in args)
        {
            var publicUrl = _cachePurge.BuildPublicUrl(target);
            Console.WriteLine($"Invalidiere CDN-Cache für {publicUrl} ...");
            var result = await _cachePurge.InvalidateUrlAsync(publicUrl, cancellationToken)
                .ConfigureAwait(false);
            Console.WriteLine($"Cache-Invalidierung ausgelöst{Format(result)}.");
        }

        return 0;
    }

    internal static string Format(PurgeResult result)
    {
        var details = result.PurgeId is null ? "" : $" (purgeId: {result.PurgeId}";
        if (result.EstimatedSeconds is int seconds)
        {
            details += details.Length == 0 ? $" (ca. {seconds}s)" : $", ca. {seconds}s)";
        }
        else if (details.Length > 0)
        {
            details += ")";
        }

        return details;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "-?" or "/?";
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Akamai Image Uploader — Cache-Purge

            Aufruf:
              AkamaiImageUploader purge <dateiname>
              AkamaiImageUploader purge <url>
              AkamaiImageUploader purge <dateiname-oder-url> [...]

            Ohne vollständige URL wird die öffentliche Adresse aus
            Akamai:CachePurge:PublicBaseUrl und dem Dateinamen gebildet.
            """);
    }
}
