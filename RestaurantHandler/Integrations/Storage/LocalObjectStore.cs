namespace RestaurantHandler.Integrations.Storage;

/// <summary>
/// Fichiers rangés sur le disque de l'API, sous <see cref="Root"/>, et servis en statique sous
/// <see cref="RequestPath"/> (Program.cs). Tout ce qui est écrit ici devient public : les fichiers
/// privés sont donc refusés, faute d'endpoint authentifié pour les servir.
/// </summary>
public class LocalObjectStore : IObjectStore
{
    public const string RequestPath = "/uploads";

    private readonly string _publicBaseUrl;

    public LocalObjectStore(string root, string publicBaseUrl)
    {
        Root = Path.GetFullPath(root);
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public async Task PutAsync(string key, Stream content, string contentType, bool isPublic, CancellationToken ct)
    {
        if (!isPublic)
            throw new NotSupportedException("Le stockage local ne sert que des fichiers publics.");

        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Écrit à côté puis renomme : un lecteur ne voit jamais un fichier à moitié écrit.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await content.CopyToAsync(file, ct);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    public string PublicUrl(string key) => $"{_publicBaseUrl}/{key}";

    public string? SignedUrl(string key) => null;

    private string PathOf(string key)
    {
        var path = Path.GetFullPath(Path.Combine(Root, key));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Clé de stockage hors du dossier local.", nameof(key));
        return path;
    }
}
