namespace Akamai.NetStorage;

public interface INetStorageClient
{
    void ValidateConfiguration();

    /// <summary>
    /// Prüft die Zugangsdaten per dir-Aufruf auf dem konfigurierten Ordner.
    /// NetStorage kennt kein klassisches Login; die HMAC-Header gelten als Authentifizierung.
    /// </summary>
    Task ValidateConnectionAsync(CancellationToken cancellationToken = default);

    Task UploadAsync(string localFilePath, string remoteFileName, CancellationToken cancellationToken = default);

    Task UploadAsync(Stream content, string remoteFileName, string? contentType = null, CancellationToken cancellationToken = default);

    Task<NetStorageListResult> ListAsync(string? relativeDirectory = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string remoteFileName, CancellationToken cancellationToken = default);

    Task<NetStorageDownload> DownloadAsync(string remoteFileName, CancellationToken cancellationToken = default);

    Task DownloadToFileAsync(string remoteFileName, string localFilePath, CancellationToken cancellationToken = default);

    string BuildPublicRelativePath(string remoteFileName);
}
