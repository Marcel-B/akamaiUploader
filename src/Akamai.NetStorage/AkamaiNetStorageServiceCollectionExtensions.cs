using Akamai.NetStorage;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class AkamaiNetStorageServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAkamaiNetStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<NetStorageOptions>(configuration);
        return AddClient(services);
    }

    public static IHttpClientBuilder AddAkamaiNetStorage(
        this IServiceCollection services,
        Action<NetStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        return AddClient(services);
    }

    private static IHttpClientBuilder AddClient(IServiceCollection services)
    {
        return services.AddHttpClient<INetStorageClient, NetStorageClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });
    }
}
