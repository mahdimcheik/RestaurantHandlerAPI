using System.Collections.Concurrent;
using System.Net;
using Amazon.Runtime;

namespace RestaurantHandler.IntegrationTests;

/// <summary>
/// Faux stockage objet branché sous le SDK AWS : chaque PUT est conservé (clé → en-têtes utiles),
/// chaque DELETE le retire. Les tests vérifient ainsi l'envoi, l'ACL et la suppression des clés.
/// </summary>
public sealed class FakeS3 : HttpMessageHandler
{
    public sealed record StoredObject(string ContentType, string? Acl, byte[] Body)
    {
        public long Length => Body.Length;
    }

    public const string Bucket = "test";

    public ConcurrentDictionary<string, StoredObject> Objects { get; } = new();
    public ConcurrentBag<string> Deleted { get; } = [];

    /// <summary>Client HTTP fourni au SDK à la place du sien.</summary>
    public HttpClientFactory Factory => new Factory_(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath).TrimStart('/');
        var key = path.StartsWith(Bucket + "/") ? path[(Bucket.Length + 1)..] : path;

        if (request.Method == HttpMethod.Put)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(ct);
            var acl = request.Headers.TryGetValues("x-amz-acl", out var values) ? values.FirstOrDefault() : null;
            var type = request.Content?.Headers.ContentType?.MediaType ?? "application/octet-stream";
            Objects[key] = new StoredObject(type, acl, body);
            var ok = new HttpResponseMessage(HttpStatusCode.OK);
            ok.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"fake\"");
            return ok;
        }

        if (request.Method == HttpMethod.Delete)
        {
            Objects.TryRemove(key, out _);
            Deleted.Add(key);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return new HttpResponseMessage(HttpStatusCode.NotImplemented);
    }

    private sealed class Factory_(FakeS3 handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }
}
