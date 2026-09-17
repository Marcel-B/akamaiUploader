using Akamai.NetStorage;

namespace Akamai.NetStorage.Tests;

public class NetStoragePathTests
{
    [Fact]
    public void Combine_encodes_segments()
    {
        var path = NetStoragePath.Combine("123 456", "images", "hero banner.png");
        Assert.Equal("/123%20456/images/hero%20banner.png", path);
    }

    [Fact]
    public void RequireFileName_rejects_parent_segments()
    {
        Assert.Throws<ArgumentException>(() => NetStoragePath.RequireFileName("../secret.png"));
        Assert.Throws<ArgumentException>(() => NetStoragePath.RequireFileName("a/../b.png"));
    }

    [Fact]
    public void ResolvedHost_appends_nsu_suffix_for_prefix()
    {
        var options = new NetStorageOptions { Host = "example" };
        Assert.Equal("example-nsu.akamaihd.net", options.ResolvedHost());
    }
}
