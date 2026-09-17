using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Akamai.NetStorage;

public sealed class NetStorageClient : INetStorageClient
{
    private readonly HttpClient _httpClient;
    private readonly NetStorageOptions _options;

    public NetStorageClient(HttpClient httpClient, IOptions<NetStorageOptions> options)
        : this(httpClient, options.Value)
    {
    }

    public NetStorageClient(HttpClient httpClient, NetStorageOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void ValidateConfiguration()
    {
        var missing = _options.MissingSettings();
        if (missing.Count > 0)
        {
            throw new NetStorageException(
                "NetStorage-Zugangsdaten fehlen: " + string.Join(", ", missing));
        }
    }

    public async Task ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var path = BuildDirectoryPath();
        using var response = await SendAsync(
            HttpMethod.Get,
            path,
            "version=1&action=dir&format=xml&encoding=utf-8",
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
        {
            return;
        }

        await ThrowForResponseAsync(response, "Verbindungstest", cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadAsync(string localFilePath, string remoteFileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);
        await using var stream = File.OpenRead(localFilePath);
        await UploadAsync(stream, remoteFileName, GuessContentType(localFilePath), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UploadAsync(
        Stream content,
        string remoteFileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateConfiguration();
        var objectName = NetStoragePath.RequireFileName(remoteFileName);
        var path = BuildObjectPath(objectName);

        await using var prepared = await PrepareUploadAsync(content, cancellationToken).ConfigureAwait(false);
        var md5 = prepared.Md5;
        var length = prepared.Length;
        var mtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var action = $"version=1&action=upload&md5={md5}&mtime={mtime}&size={length}";

        using var httpContent = new StreamContent(prepared.Stream);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        httpContent.Headers.ContentLength = length;

        using var response = await SendAsync(HttpMethod.Put, path, action, httpContent, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
        {
            return;
        }

        await ThrowForResponseAsync(response, "Upload", cancellationToken).ConfigureAwait(false);
    }

    public async Task<NetStorageListResult> ListAsync(
        string? relativeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var path = string.IsNullOrWhiteSpace(relativeDirectory)
            ? BuildDirectoryPath()
            : BuildObjectPath(relativeDirectory);

        using var response = await SendAsync(
            HttpMethod.Get,
            path,
            "version=1&action=dir&format=xml&encoding=utf-8",
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return DirResponseParser.Parse(xml);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new NetStorageListResult(path, []);
        }

        await ThrowForResponseAsync(response, "Dateiliste", cancellationToken).ConfigureAwait(false);
        return new NetStorageListResult(path, []);
    }

    public async Task DeleteAsync(string remoteFileName, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var objectName = NetStoragePath.RequireFileName(remoteFileName);
        var path = BuildObjectPath(objectName);

        using var response = await SendAsync(
            HttpMethod.Put,
            path,
            "version=1&action=delete",
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
        {
            return;
        }

        await ThrowForResponseAsync(response, "Löschen", cancellationToken).ConfigureAwait(false);
    }

    public async Task<NetStorageDownload> DownloadAsync(
        string remoteFileName,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var objectName = NetStoragePath.RequireFileName(remoteFileName);
        var path = BuildObjectPath(objectName);

        var response = await SendAsync(
            HttpMethod.Get,
            path,
            "version=1&action=download",
            content: null,
            cancellationToken).ConfigureAwait(false);

        try
        {
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var fileName = Path.GetFileName(objectName.Replace('\\', '/'));
                return new NetStorageDownload(response, stream, fileName);
            }

            await ThrowForResponseAsync(response, "Download", cancellationToken).ConfigureAwait(false);
            throw new NetStorageException("Download fehlgeschlagen.");
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task DownloadToFileAsync(
        string remoteFileName,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);
        await using var download = await DownloadAsync(remoteFileName, cancellationToken).ConfigureAwait(false);
        var destination = localFilePath;
        if (Directory.Exists(destination) || destination.EndsWith(Path.DirectorySeparatorChar) || destination.EndsWith(Path.AltDirectorySeparatorChar))
        {
            destination = Path.Combine(destination, download.FileName);
        }

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(destination);
        await download.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public string BuildPublicRelativePath(string remoteFileName)
    {
        var objectName = NetStoragePath.RequireFileName(remoteFileName);
        var remotePath = _options.RemotePath.Trim().Trim('/');
        return string.IsNullOrEmpty(remotePath)
            ? objectName
            : $"{remotePath}/{objectName}";
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestPath,
        string actionHeader,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var url = $"https://{_options.ResolvedHost()}{requestPath}";
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.ExpectContinue = false;
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");

        var (authData, authSign) = NetStorageSigner.Sign(
            requestPath,
            actionHeader,
            _options.UploadAccountId,
            _options.Key);

        request.Headers.TryAddWithoutValidation("X-Akamai-ACS-Action", actionHeader);
        request.Headers.TryAddWithoutValidation("X-Akamai-ACS-Auth-Data", authData);
        request.Headers.TryAddWithoutValidation("X-Akamai-ACS-Auth-Sign", authSign);

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }

    private string BuildDirectoryPath() => NetStoragePath.Combine(_options.CpCode, _options.RemotePath);

    private string BuildObjectPath(string relativePath) =>
        NetStoragePath.Combine(_options.CpCode, _options.RemotePath, relativePath);

    private static async Task ThrowForResponseAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new NetStorageException(
                $"{operation} bei NetStorage fehlgeschlagen: Zugangsdaten prüfen (Host, CpCode, UploadAccountId, Key) und die Systemzeit (NTP, Abweichung max. 60 Sekunden).",
                response.StatusCode);
        }

        throw new NetStorageException(
            $"{operation} bei NetStorage fehlgeschlagen ({(int)response.StatusCode} {response.ReasonPhrase}): {body}",
            response.StatusCode);
    }

    private static async Task<PreparedUpload> PrepareUploadAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            var start = content.Position;
            var md5 = Convert.ToHexString(await MD5.HashDataAsync(content, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
            var length = content.Length - start;
            content.Position = start;
            return new PreparedUpload(content, md5, length, ownsStream: false);
        }

        var tempPath = Path.GetTempFileName();
        var temp = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await content.CopyToAsync(temp, cancellationToken).ConfigureAwait(false);
            temp.Position = 0;
            var md5 = Convert.ToHexString(await MD5.HashDataAsync(temp, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
            temp.Position = 0;
            return new PreparedUpload(temp, md5, temp.Length, ownsStream: true);
        }
        catch
        {
            await temp.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static string GuessContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".avif" => "image/avif",
            _ => "application/octet-stream"
        };
    }

    private sealed class PreparedUpload : IAsyncDisposable
    {
        public PreparedUpload(Stream stream, string md5, long length, bool ownsStream)
        {
            Stream = stream;
            Md5 = md5;
            Length = length;
            _ownsStream = ownsStream;
        }

        public Stream Stream { get; }
        public string Md5 { get; }
        public long Length { get; }
        private readonly bool _ownsStream;

        public ValueTask DisposeAsync()
        {
            return _ownsStream ? Stream.DisposeAsync() : ValueTask.CompletedTask;
        }
    }
}
