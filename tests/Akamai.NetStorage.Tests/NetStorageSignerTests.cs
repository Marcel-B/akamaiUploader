using Akamai.NetStorage;

namespace Akamai.NetStorage.Tests;

public class NetStorageSignerTests
{
    [Fact]
    public void Sign_matches_akamai_documentation_example()
    {
        var (authData, authSign) = NetStorageSigner.Sign(
            requestPath: "/123456/files_baseball/sweep.m4a",
            actionHeader: "version=1&action=upload&md5=0123456789abcdef0123456789abcdef&mtime=1260000000",
            uploadAccountId: "UploadAccountMedia",
            key: "abcdefghij",
            epochSeconds: 1280000000,
            uniqueId: 382644692);

        Assert.Equal("5, 0.0.0.0, 0.0.0.0, 1280000000, 382644692, UploadAccountMedia", authData);
        Assert.Equal("yh1MXm/rv7RKZhfKlTuSUBV69Acph5IyOWCU0/nFjms=", authSign);
    }
}
