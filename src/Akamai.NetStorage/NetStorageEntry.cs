namespace Akamai.NetStorage;

public enum NetStorageEntryType
{
    File,
    Directory,
    Symlink,
    Unknown
}

public sealed record NetStorageEntry(
    string Name,
    NetStorageEntryType Type,
    long? Size = null,
    string? Md5 = null,
    DateTimeOffset? ModifiedAt = null,
    bool IsImplicitDirectory = false,
    string? SymlinkTarget = null,
    int? DirectoryFileCount = null);
