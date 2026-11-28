namespace RestaurantHandler.Integrations.Storage;

/// <summary>
/// Backend de <see cref="StorageService"/>, choisi par STORAGE_PROVIDER : <see cref="S3ObjectStore"/>
/// (compartiment S3) ou <see cref="LocalObjectStore"/> (disque de l'API). Les clés reçues sont déjà
/// validées par <see cref="StorageService.IsValidKey"/>.
/// </summary>
public interface IObjectStore
{
    Task PutAsync(string key, Stream content, string contentType, bool isPublic, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);

    string PublicUrl(string key);

    /// <summary>Lien de lecture temporaire d'un fichier privé, ou null si le backend n'en sert pas.</summary>
    string? SignedUrl(string key);
}
