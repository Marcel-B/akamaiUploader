using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AkamaiImageUploader.Configuration;
using Microsoft.Extensions.Options;

namespace AkamaiImageUploader.Purge;

public sealed class CachePurgeClient
{
    private readonly HttpClient _httpClient;
    private readonly CachePurgeOptions _options;

    public CachePurgeClient(HttpClient httpClient, IOptions<AkamaiOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value.CachePurge;
    }

    public bool IsConfigured => _options.Enabled && _options.HasCredentials();

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Cache-Purge ist nicht konfiguriert: EdgeGrid-Zugangsdaten fehlen oder CachePurge ist deaktiviert.");
        }
    }

    public string BuildPublicUrl(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (IsAbsoluteUrl(fileName))
        {
            return fileName.Trim();
        }

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{fileName.Trim().TrimStart('/')}";
    }

    public static bool IsAbsoluteUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public Task<PurgeResult> InvalidateFileAsync(string fileName, CancellationToken cancellationToken)
    {
        return InvalidateUrlAsync(BuildPublicUrl(fileName), cancellationToken);
    }

    public async Task<PurgeResult> InvalidateUrlAsync(string publicUrl, CancellationToken cancellationToken)
    {
        var host = _options.ResolvedHost();
        var network = _options.ResolvedNetwork();
        var requestUri = new Uri($"https://{host}/ccu/v3/invalidate/url/{network}");
        var payload = JsonSerializer.Serialize(new { objects = new[] { publicUrl } });
        var body = Encoding.UTF8.GetBytes(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            EdgeGridSigner.CreateAuthorizationHeader(
                "POST",
                requestUri,
                body,
                _options.ClientToken,
                _options.ClientSecret,
                _options.AccessToken));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cache-Invalidierung fehlgeschlagen ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody);
        var root = document.RootElement;
        return new PurgeResult(
            root.TryGetProperty("purgeId", out var purgeId) ? purgeId.GetString() : null,
            root.TryGetProperty("estimatedSeconds", out var seconds) ? seconds.GetInt32() : null);
    }
}

public sealed record PurgeResult(string? PurgeId, int? EstimatedSeconds);
