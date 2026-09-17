using AkamaiImageUploader;

namespace AkamaiImageUploader.Tests;

public class UploadCommandTests
{
    [Fact]
    public void ResolveRemoteFileName_appends_source_extension_when_missing()
    {
        var name = UploadCommand.ResolveRemoteFileName("hero-banner", "/tmp/photo.png");
        Assert.Equal("hero-banner.png", name);
    }

    [Fact]
    public void ResolveRemoteFileName_keeps_explicit_extension()
    {
        var name = UploadCommand.ResolveRemoteFileName("logo.webp", "/tmp/photo.png");
        Assert.Equal("logo.webp", name);
    }
}
