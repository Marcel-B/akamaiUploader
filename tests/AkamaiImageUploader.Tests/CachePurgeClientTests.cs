using AkamaiImageUploader.Configuration;
using AkamaiImageUploader.Purge;
using Microsoft.Extensions.Options;

namespace AkamaiImageUploader.Tests;

public class CachePurgeClientTests
{
    [Fact]
    public void BuildPublicUrl_appends_file_name_to_base_url()
    {
        var client = CreateClient("https://www.example.com/images");
        Assert.Equal("https://www.example.com/images/hero.png", client.BuildPublicUrl("hero.png"));
    }

    [Fact]
    public void BuildPublicUrl_keeps_absolute_url()
    {
        var client = CreateClient("https://www.example.com/images");
        Assert.Equal(
            "https://cdn.example.com/assets/hero.png",
            client.BuildPublicUrl("https://cdn.example.com/assets/hero.png"));
    }

    [Fact]
    public void EnsureConfigured_throws_when_credentials_missing()
    {
        var client = CreateClient("https://www.example.com/images");
        var ex = Assert.Throws<InvalidOperationException>(client.EnsureConfigured);
        Assert.Contains("nicht konfiguriert", ex.Message);
    }

    [Fact]
    public void Format_includes_purge_id_and_estimated_seconds()
    {
        var text = PurgeCommand.Format(new PurgeResult("abc", 8));
        Assert.Equal(" (purgeId: abc, ca. 8s)", text);
    }

    private static CachePurgeClient CreateClient(string publicBaseUrl)
    {
        var options = Options.Create(new AkamaiOptions
        {
            CachePurge = new CachePurgeOptions { PublicBaseUrl = publicBaseUrl }
        });
        return new CachePurgeClient(new HttpClient(), options);
    }
}
