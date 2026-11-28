using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Integrations.Storage;

/// <summary>
/// Seule porte vers le stockage de fichiers (ADR-0003), vers le backend choisi par STORAGE_PROVIDER
/// (<see cref="IObjectStore"/> : S3 ou disque local). Les avatars et la carte (préfixes
/// <see cref="AvatarFolder"/> et <see cref="CatalogFolder"/>, en lecture anonyme) sont servis par URL
/// publique stable, tout le reste par lien signé. Seule la clé est persistée ; l'URL est calculée à la lecture.
/// </summary>
public class StorageService(IObjectStore store, ILogger<StorageService> logger)
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

    /// <summary>
    /// Contenu produit par l'API, envoyé à une clé choisie par l'appelant (variantes d'une même photo
    /// qui partagent un GUID). La clé doit provenir de <see cref="BuildKey"/>.
    /// </summary>
    public async Task<Response<string>> UploadBytesAt(byte[] content, string key, CancellationToken ct)
    {
        if (content.Length == 0)
            return Fail(400, "Aucun fichier reçu.");
        if (!IsValidKey(key))
            return Fail(400, "Clé de stockage invalide.");

        using var stream = new MemoryStream(content, writable: false);
        return await Put(key, stream, ct);
    }

    /// <summary>Idempotent ; un échec est journalisé, jamais propagé (au pire un fichier orphelin).</summary>
    public async Task Delete(string key, CancellationToken ct)
    {
        if (!IsValidKey(key))
            return;

        try
        {
            await store.DeleteAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Suppression de {Key} impossible", key);
        }
    }

    /// <summary>
    /// URL publique stable pour un fichier public, lien signé pour tout autre fichier, ou null pour
    /// une clé absente ou invalide (ou un fichier privé que le backend ne sait pas servir).
    /// </summary>
    public string? Url(string? key)
    {
        if (!IsValidKey(key))
            return null;

        return IsPublic(key!) ? store.PublicUrl(key!) : store.SignedUrl(key!);
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
            await store.PutAsync(key, content, ContentTypeFor(key), IsPublic(key), ct);

            return new Response<string> { Status = 201, Message = "Fichier téléversé.", Data = key };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Téléversement de {Key} impossible", key);
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
