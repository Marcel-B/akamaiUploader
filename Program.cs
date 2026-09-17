using AkamaiImageUploader;
using AkamaiImageUploader.Configuration;
using AkamaiImageUploader.NetStorage;
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
builder.Services.AddHttpClient("NetStorage", client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddHttpClient("EdgeGrid", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<NetStorageClient>();
builder.Services.AddSingleton<CachePurgeClient>();
builder.Services.AddSingleton<UploadCommand>();

using var host = builder.Build();
var exitCode = await host.Services.GetRequiredService<UploadCommand>()
    .ExecuteAsync(args, CancellationToken.None)
    .ConfigureAwait(false);

return exitCode;
