using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Akamai.NetStorage;

namespace Akamai.NetStorage.Tests;

public class NetStorageClientTests
{
    private static readonly NetStorageOptions Options = new()
    {
        Host = "example-nsu.akamaihd.net",
        CpCode = "123456",
        UploadAccountId = "account",
        Key = "secret",
        RemotePath = "images"
    };

    [Fact]
    public async Task ListAsync_sends_dir_action_and_parses_xml()
    {
        var xml =
            """
            <stat directory="/123456/images">
              <file type="file" name="hero.png" size="12" mtime="1"/>
            </stat>
            """;
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        });
        var client = CreateClient(handler);

        var result = await client.ListAsync();

        Assert.Equal("GET", handler.Request!.Method.Method);
        Assert.Equal("https://example-nsu.akamaihd.net/123456/images", handler.Request.RequestUri!.ToString());
        Assert.Equal("version=1&action=dir&format=xml&encoding=utf-8", ActionHeader(handler));
        Assert.Contains("hero.png", result.Entries.Select(e => e.Name));
    }

    [Fact]
    public async Task DeleteAsync_sends_put_delete_action()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.DeleteAsync("hero.png");

        Assert.Equal("PUT", handler.Request!.Method.Method);
        Assert.Equal("https://example-nsu.akamaihd.net/123456/images/hero.png", handler.Request.RequestUri!.ToString());
        Assert.Equal("version=1&action=delete", ActionHeader(handler));
    }

    [Fact]
    public async Task DownloadAsync_returns_file_bytes()
    {
        var payload = "image-bytes"u8.ToArray();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var handler = new StubHandler(response);
        var client = CreateClient(handler);

        await using var download = await client.DownloadAsync("hero.png");
        using var memory = new MemoryStream();
        await download.Content.CopyToAsync(memory);

        Assert.Equal("GET", handler.Request!.Method.Method);
        Assert.Equal("version=1&action=download", ActionHeader(handler));
        Assert.Equal("hero.png", download.FileName);
        Assert.Equal("image/png", download.ContentType);
        Assert.Equal(payload, memory.ToArray());
    }

    [Fact]
    public async Task UploadAsync_sends_put_upload_action_with_md5()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, "hello"u8.ToArray());

        try
        {
            await client.UploadAsync(tempFile, "hello.txt");
        }
        finally
        {
            File.Delete(tempFile);
        }

        Assert.Equal("PUT", handler.Request!.Method.Method);
        Assert.Equal("https://example-nsu.akamaihd.net/123456/images/hello.txt", handler.Request.RequestUri!.ToString());
        Assert.StartsWith("version=1&action=upload&md5=", ActionHeader(handler));
        Assert.Contains("&size=5", ActionHeader(handler));
    }

    [Fact]
    public void BuildPublicRelativePath_includes_remote_path()
    {
        var client = CreateClient(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        Assert.Equal("images/hero.png", client.BuildPublicRelativePath("hero.png"));
    }

    [Fact]
    public void ValidateConfiguration_throws_when_settings_missing()
    {
        var client = new NetStorageClient(new HttpClient(), new NetStorageOptions());
        var ex = Assert.Throws<NetStorageException>(client.ValidateConfiguration);
        Assert.Contains("Host", ex.Message);
        Assert.Contains("CpCode", ex.Message);
    }

    private static NetStorageClient CreateClient(StubHandler handler)
    {
        return new NetStorageClient(new HttpClient(handler), Options);
    }

    private static string ActionHeader(StubHandler handler)
    {
        return handler.Request!.Headers.GetValues("X-Akamai-ACS-Action").Single();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public StubHandler(HttpResponseMessage response)
        {
            Response = response;
        }

        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
