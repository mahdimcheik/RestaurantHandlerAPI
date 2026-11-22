using Amazon.S3;
using Amazon.S3.Model;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Integrations.Storage;

/// <summary>
/// Seule porte vers le stockage objet (ADR-0008) : un compartiment S3 unique (OVH, MinIO en local). Les avatars et la carte
/// (préfixes <see cref="AvatarFolder"/> et <see cref="CatalogFolder"/>, en lecture anonyme) sont servis par URL publique stable, tout le reste
/// par lien signé. Seule la clé d'objet est persistée ; l'URL est calculée à la lecture.
/// </summary>
public class StorageService(IAmazonS3 s3, ILogger<StorageService> logger)
{
    /// <summary>Dossiers publics : seuls leurs objets sont envoyés avec l'ACL public-read.</summary>
    public const string AvatarFolder = "avatars";

    /// <summary>Photos de la carte et logo du restaurant.</summary>
    public const string CatalogFolder = "catalog";

    private static readonly Dictionary<string, string> ContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".csv"] = "text/csv",
            [".txt"] = "text/plain",
            [".mp4"] = "video/mp4",
        };

    /// <summary>Fichier envoyé par un client : taille, extension et signature vérifiées. Renvoie la clé.</summary>
    public async Task<Response<string>> Upload(IFormFile file, string folder, CancellationToken ct)
    {
        if (await ValidateAsync(file, ct) is { } invalid)
            return invalid;

        await using var content = file.OpenReadStream();
        return await Put(BuildKey(folder, Path.GetExtension(file.FileName)), content, ct);
    }

    /// <summary>Contenu produit par l'API (facture, avatar recadré) : pas de validation. Renvoie la clé.</summary>
    public async Task<Response<string>> UploadBytes(
        byte[] content,
        string folder,
        string extension,
        CancellationToken ct
    )
    {
        if (content.Length == 0)
            return Fail(400, "Aucun fichier reçu.");

        using var stream = new MemoryStream(content, writable: false);
        return await Put(BuildKey(folder, extension), stream, ct);
    }

    /// <summary>Idempotent ; un échec est journalisé, jamais propagé (au pire un fichier orphelin).</summary>
    public async Task Delete(string key, CancellationToken ct)
    {
        if (!IsValidKey(key))
            return;

        try
        {
            await s3.DeleteObjectAsync(EnvironmentVariables.S3_BUCKET, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Suppression de {Key} impossible", key);
        }
    }

    /// <summary>
    /// URL publique stable pour un avatar, lien GET signé pour tout autre fichier, ou null pour une
    /// clé absente ou invalide. La signature est calculée localement (aucun appel réseau) et lie
    /// l'hôte : S3_UPLOAD_URL doit être joignable par les navigateurs.
    /// </summary>
    public string? Url(string? key)
    {
        if (!IsValidKey(key))
            return null;

        if (IsPublic(key!))
            return $"{EnvironmentVariables.S3_PUBLIC_URL.TrimEnd('/')}/{key}";

        return s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = EnvironmentVariables.S3_BUCKET,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(EnvironmentVariables.SIGNED_URL_MINUTES),
        });
    }

    /// <summary>
    /// Une clé doit être de celles que <see cref="BuildKey"/> produit : rejette traversée,
    /// chemins absolus et caractères hors [a-z0-9-_./].
    /// </summary>
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= 512 &&
        !key.StartsWith('/') &&
        !key.Contains("..") &&
        !key.Contains("//") &&
        key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/');

    /// <summary>
    /// "{folder}/{yyyy}/{MM}/{guid}{ext}". Le nom d'origine n'entre jamais dans la clé,
    /// seulement son extension.
    /// </summary>
    public static string BuildKey(string folder, string extension)
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        var segments = folder
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => new string(s.ToLowerInvariant()
                .Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
                .ToArray()))
            .Where(s => s.Length > 0);
        var prefix = string.Join('/', segments);
        var now = DateTimeOffset.UtcNow;

        return $"{(prefix.Length > 0 ? prefix + "/" : "")}{now:yyyy}/{now:MM}/{Guid.NewGuid()}{ext.ToLowerInvariant()}";
    }

    private static bool IsPublic(string key) =>
        key.StartsWith(AvatarFolder + "/", StringComparison.Ordinal)
        || key.StartsWith(CatalogFolder + "/", StringComparison.Ordinal);

    public static string ContentTypeFor(string keyOrFileName) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(keyOrFileName), "application/octet-stream");

    private async Task<Response<string>> Put(string key, Stream content, CancellationToken ct)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = EnvironmentVariables.S3_BUCKET,
                Key = key,
                InputStream = content,
                ContentType = ContentTypeFor(key),
                AutoCloseStream = false,
            };
            // Une clé publique contient un GUID et n'est jamais réécrite : cache illimité sans risque.
            if (IsPublic(key))
            {
                request.Headers.CacheControl = "public, max-age=31536000, immutable";
                request.CannedACL = S3CannedACL.PublicRead;
            }

            await s3.PutObjectAsync(request, ct);

            return new Response<string> { Status = 201, Message = "Fichier téléversé.", Data = key };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Téléversement de {Key} vers {Endpoint} impossible", key, EnvironmentVariables.S3_UPLOAD_URL);
            return Fail(503, "Stockage indisponible.");
        }
    }

    private static async Task<Response<string>?> ValidateAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Fail(400, "Aucun fichier reçu.");

        if (file.Length > EnvironmentVariables.MaxUploadSizeBytes)
            return Fail(413, $"Fichier trop volumineux (maximum {EnvironmentVariables.MAX_UPLOAD_SIZE_MB} Mo).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = EnvironmentVariables.ALLOWED_EXTENSIONS
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : $".{e}");
        if (string.IsNullOrEmpty(extension) ||
            !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
            !FileSignatures.IsKnown(extension))
            return Fail(415, "Type de fichier non autorisé.");

        await using var stream = file.OpenReadStream();
        if (!await FileSignatures.MatchesAsync(stream, extension, ct))
            return Fail(415, "Le contenu du fichier ne correspond pas à son extension.");

        return null;
    }

    private static Response<string> Fail(int status, string message) =>
        new() { Status = status, Message = message };
}
