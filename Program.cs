using AkamaiImageUploader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddAkamaiImageUploader(configuration);

using var provider = services.BuildServiceProvider();
return await CommandDispatcher.ExecuteAsync(provider, args).ConfigureAwait(false);
