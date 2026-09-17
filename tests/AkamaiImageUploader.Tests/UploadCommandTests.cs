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

    [Fact]
    public void AppendTimestamp_inserts_timestamp_before_extension()
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 15, 30, 12, TimeSpan.Zero);
        var name = UploadCommand.AppendTimestamp("hero-banner.png", timestamp);
        Assert.Equal("hero-banner_20260917153012.png", name);
    }

    [Fact]
    public void AppendTimestamp_works_without_extension()
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 15, 30, 12, TimeSpan.Zero);
        var name = UploadCommand.AppendTimestamp("hero-banner", timestamp);
        Assert.Equal("hero-banner_20260917153012", name);
    }
}
