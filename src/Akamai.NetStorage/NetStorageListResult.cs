namespace Akamai.NetStorage;

public sealed record NetStorageListResult(
    string Directory,
    IReadOnlyList<NetStorageEntry> Entries,
    string? ResumeStart = null);
