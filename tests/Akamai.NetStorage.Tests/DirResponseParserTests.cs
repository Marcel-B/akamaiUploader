using Akamai.NetStorage;

namespace Akamai.NetStorage.Tests;

public class DirResponseParserTests
{
    [Fact]
    public void Parse_reads_files_directories_and_symlinks()
    {
        const string xml =
            """
            <stat directory="/1234/images">
              <file type="file" name="logo.png" size="398421" md5="abc" mtime="1524068379"/>
              <file type="symlink" name="alias.png" target="logo.png" mtime="1524110333"/>
              <file type="dir" name="thumbs" bytes="19873716" files="6" mtime="1524068415" implicit="true"/>
              <resume start="thumbs"/>
            </stat>
            """;

        var result = DirResponseParser.Parse(xml);

        Assert.Equal("/1234/images", result.Directory);
        Assert.Equal("thumbs", result.ResumeStart);
        Assert.Equal(3, result.Entries.Count);

        Assert.Equal("logo.png", result.Entries[0].Name);
        Assert.Equal(NetStorageEntryType.File, result.Entries[0].Type);
        Assert.Equal(398421, result.Entries[0].Size);
        Assert.Equal("abc", result.Entries[0].Md5);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1524068379), result.Entries[0].ModifiedAt);

        Assert.Equal(NetStorageEntryType.Symlink, result.Entries[1].Type);
        Assert.Equal("logo.png", result.Entries[1].SymlinkTarget);

        Assert.Equal(NetStorageEntryType.Directory, result.Entries[2].Type);
        Assert.True(result.Entries[2].IsImplicitDirectory);
        Assert.Equal(19873716, result.Entries[2].Size);
        Assert.Equal(6, result.Entries[2].DirectoryFileCount);
    }

    [Fact]
    public void Parse_decodes_base64_names()
    {
        var encoded = Convert.ToBase64String("café.png"u8.ToArray());
        var xml = $"<stat directory=\"/1\"><file type=\"file\" name_base64=\"{encoded}\" size=\"1\"/></stat>";

        var result = DirResponseParser.Parse(xml);

        Assert.Equal("café.png", result.Entries[0].Name);
    }
}
