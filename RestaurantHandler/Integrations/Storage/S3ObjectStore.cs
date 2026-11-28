using Amazon.S3;
using Amazon.S3.Model;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Integrations.Storage;

/// <summary>
/// Compartiment S3 unique (OVH Object Storage) : les fichiers publics sont envoyés en public-read
/// et lus via S3_PUBLIC_URL, les autres par lien signé (signature locale, liée à l'hôte de
/// S3_UPLOAD_URL, qui doit donc être joignable par les navigateurs).
/// </summary>
public class S3ObjectStore(IAmazonS3 s3) : IObjectStore
{
    public async Task PutAsync(string key, Stream content, string contentType, bool isPublic, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = EnvironmentVariables.S3_BUCKET,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        // Une clé publique contient un GUID et n'est jamais réécrite : cache illimité sans risque.
        if (isPublic)
        {
            request.Headers.CacheControl = "public, max-age=31536000, immutable";
            request.CannedACL = S3CannedACL.PublicRead;
        }

        await s3.PutObjectAsync(request, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(EnvironmentVariables.S3_BUCKET, key, ct);

    public string PublicUrl(string key) => $"{EnvironmentVariables.S3_PUBLIC_URL.TrimEnd('/')}/{key}";

    public string? SignedUrl(string key) =>
        s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = EnvironmentVariables.S3_BUCKET,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(EnvironmentVariables.SIGNED_URL_MINUTES),
        });
}
