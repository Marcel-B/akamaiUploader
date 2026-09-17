using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using AkamaiImageUploader.Configuration;
using Microsoft.Extensions.Options;

namespace AkamaiImageUploader.NetStorage;

public sealed class NetStorageClient
{
    private readonly HttpClient _httpClient;
    private readonly NetStorageOptions _options;

    public NetStorageClient(IHttpClientFactory httpClientFactory, IOptions<AkamaiOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("NetStorage");
        _options = options.Value.NetStorage;
    }

    public void ValidateConfiguration()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.ResolvedHost()))
        {
            missing.Add("Akamai:NetStorage:Host");
        }

        if (string.IsNullOrWhiteSpace(_options.CpCode))
        {
            missing.Add("Akamai:NetStorage:CpCode");
        }

        if (string.IsNullOrWhiteSpace(_options.UploadAccountId))
        {
            missing.Add("Akamai:NetStorage:UploadAccountId");
        }

        if (string.IsNullOrWhiteSpace(_options.Key))
        {
            missing.Add("Akamai:NetStorage:Key");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "NetStorage-Zugangsdaten fehlen in appsettings.json: " + string.Join(", ", missing));
        }
    }

    /// <summary>
    /// Prüft die Zugangsdaten per dir-Aufruf auf dem Zielordner.
    /// NetStorage kennt kein klassisches Login; die HMAC-Header gelten als Authentifizierung.
    /// </summary>
    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        var path = BuildDirectoryPath();
        using var response = await SendAsync(
            HttpMethod.Get,
            path,
            "version=1&action=dir&format=xml",
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "Login bei NetStorage fehlgeschlagen: Zugangsdaten prüfen (Host, CpCode, UploadAccountId, Key) und die Systemzeit (NTP, Abweichung max. 60 Sekunden).");
        }

        throw new InvalidOperationException(
            $"Login bei NetStorage fehlgeschlagen ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
    }

    public async Task UploadAsync(string localFilePath, string remoteFileName, CancellationToken cancellationToken)
    {
        var path = BuildObjectPath(remoteFileName);
        await using var stream = File.OpenRead(localFilePath);
        var md5 = Convert.ToHexString(await MD5.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();
        stream.Position = 0;

        var mtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var action = $"version=1&action=upload&md5={md5}&mtime={mtime}&size={stream.Length}";

        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(localFilePath));
        content.Headers.ContentLength = stream.Length;

        using var response = await SendAsync(HttpMethod.Put, path, action, content, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(
            $"Upload fehlgeschlagen ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
    }

    public string BuildPublicRelativePath(string remoteFileName)
    {
        var remotePath = _options.RemotePath.Trim().Trim('/');
        return string.IsNullOrEmpty(remotePath)
            ? remoteFileName
            : $"{remotePath}/{remoteFileName}";
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

    private string BuildDirectoryPath()
    {
        var segments = new List<string> { _options.CpCode.Trim('/') };
        segments.AddRange(SplitRemotePath());
        return ToRequestPath(segments);
    }

    private string BuildObjectPath(string remoteFileName)
    {
        var segments = new List<string> { _options.CpCode.Trim('/') };
        segments.AddRange(SplitRemotePath());
        segments.Add(remoteFileName);
        return ToRequestPath(segments);
    }

    private IEnumerable<string> SplitRemotePath()
    {
        return _options.RemotePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ToRequestPath(IEnumerable<string> segments)
    {
        return "/" + string.Join("/", segments.Select(Uri.EscapeDataString));
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
}
