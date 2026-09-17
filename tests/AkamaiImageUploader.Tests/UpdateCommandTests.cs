using AkamaiImageUploader;

namespace AkamaiImageUploader.Tests;

public class UpdateCommandTests
{
    [Fact]
    public void DeriveDesignation_strips_previous_timestamp_suffix()
    {
        var designation = UpdateCommand.DeriveDesignation("hero-banner_20260101120000.png");
        Assert.Equal("hero-banner", designation);
    }

    [Fact]
    public void DeriveDesignation_keeps_name_without_timestamp_suffix()
    {
        var designation = UpdateCommand.DeriveDesignation("hero-banner.png");
        Assert.Equal("hero-banner", designation);
    }
}
