using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Http
{
    public enum ApiHttpMethod
    {
        Get,
        Post,
    }

    public sealed class TransportRequest
    {
        public TransportRequest(ApiHttpMethod method, Uri uri, string? body = null, string? contentType = null)
        {
            Method = method;
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Body = body;
            ContentType = contentType;
        }

        public ApiHttpMethod Method { get; }

        public Uri Uri { get; }

        public string? Body { get; }

        public string? ContentType { get; }
    }

    public sealed class TransportResponse
    {
        public TransportResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public int StatusCode { get; }

        public string Body { get; }
    }

    /// <summary>
    /// Raw HTTP abstraction: send one request, get status + decoded text. No retries, no JSON, no
    /// policy; those live in <see cref="ApiHttpClient"/>. Throws on transport failures and honours the token.
    /// </summary>
    public interface IHttpTransport
    {
        Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken);
    }
}
