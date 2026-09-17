using System.Net;

namespace Akamai.NetStorage;

public sealed class NetStorageException : InvalidOperationException
{
    public NetStorageException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
