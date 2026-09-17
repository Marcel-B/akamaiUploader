using AkamaiImageUploader;
using AkamaiImageUploader.Configuration;
using AkamaiImageUploader.Purge;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAkamaiImageUploader(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var akamai = configuration.GetSection(AkamaiOptions.SectionName);
        services.Configure<AkamaiOptions>(akamai);
        services.AddAkamaiNetStorage(akamai.GetSection("NetStorage"));
        services.AddHttpClient<CachePurgeClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddTransient<UploadCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<ListCommand>();
        services.AddTransient<DeleteCommand>();
        services.AddTransient<DownloadCommand>();
        services.AddTransient<PurgeCommand>();
        return services;
    }
}
