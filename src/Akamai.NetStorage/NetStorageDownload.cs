using System.Net.Http.Headers;

namespace Akamai.NetStorage;

/// <summary>
/// Antwort eines Downloads. Das Stream-Dispose schließt auch die HTTP-Antwort.
/// Geeignet für ASP.NET <c>FileStreamResult</c>.
/// </summary>
public sealed class NetStorageDownload : IAsyncDisposable, IDisposable
{
    private readonly HttpResponseMessage _response;
    private bool _disposed;

    internal NetStorageDownload(HttpResponseMessage response, Stream content, string fileName)
    {
        _response = response;
        FileName = fileName;
        Content = new DisposingResponseStream(content, response, this);
        ContentType = response.Content.Headers.ContentType?.MediaType;
        ContentLength = response.Content.Headers.ContentLength;
    }

    public Stream Content { get; }

    public string FileName { get; }

    public string? ContentType { get; }

    public long? ContentLength { get; }

    public MediaTypeHeaderValue? MediaType => _response.Content.Headers.ContentType;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Content.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await Content.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    internal void MarkDisposed() => _disposed = true;
}
