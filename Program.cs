using AkamaiImageUploader;
using AkamaiImageUploader.Configuration;
using AkamaiImageUploader.Purge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Services.Configure<AkamaiOptions>(builder.Configuration.GetSection(AkamaiOptions.SectionName));
builder.Services.AddAkamaiNetStorage(builder.Configuration.GetSection("Akamai:NetStorage"));
builder.Services.AddHttpClient("EdgeGrid", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<CachePurgeClient>();
builder.Services.AddTransient<UploadCommand>();
builder.Services.AddTransient<ListCommand>();
builder.Services.AddTransient<DeleteCommand>();
builder.Services.AddTransient<DownloadCommand>();

using var host = builder.Build();
var exitCode = await DispatchAsync(host.Services, args).ConfigureAwait(false);
return exitCode;

static async Task<int> DispatchAsync(IServiceProvider services, string[] args)
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
                .ExecuteAsync(rest, CancellationToken.None).ConfigureAwait(false),
            "delete" or "rm" => await services.GetRequiredService<DeleteCommand>()
                .ExecuteAsync(rest, CancellationToken.None).ConfigureAwait(false),
            "download" or "get" => await services.GetRequiredService<DownloadCommand>()
                .ExecuteAsync(rest, CancellationToken.None).ConfigureAwait(false),
            "upload" => await services.GetRequiredService<UploadCommand>()
                .ExecuteAsync(rest, CancellationToken.None).ConfigureAwait(false),
            _ => await services.GetRequiredService<UploadCommand>()
                .ExecuteAsync(args, CancellationToken.None).ConfigureAwait(false)
        };
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or ArgumentException)
    {
        Console.Error.WriteLine($"Fehler: {ex.Message}");
        return 1;
    }
}

static bool IsHelp(string value)
{
    return value is "-h" or "--help" or "-?" or "/?";
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        Akamai Image Uploader

        Aufruf:
          AkamaiImageUploader <bildpfad> <bezeichnung>
          AkamaiImageUploader upload <bildpfad> <bezeichnung>
          AkamaiImageUploader list [unterordner]
          AkamaiImageUploader delete <dateiname>
          AkamaiImageUploader download <dateiname> [zielpfad]

        Argumente:
          bildpfad      Pfad zur lokalen Bilddatei
          bezeichnung   Freier Name in NetStorage. Fehlt die Dateiendung,
                        wird die Endung der Quelldatei übernommen.
          dateiname     Name der Datei relativ zu Akamai:NetStorage:RemotePath.

        Zugangsdaten stehen in appsettings.json (Abschnitt Akamai).
        Nach dem Upload wird der CDN-Cache für die öffentliche URL invalidiert,
        damit ein erneuter Upload unter gleichem Namen nicht das alte Bild ausliefert.
        """);
}
