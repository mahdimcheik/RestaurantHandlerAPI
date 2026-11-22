using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantHandler.Services;

/// <summary>
/// Photos de la carte et logo : formats et taille contrôlés, image redécodée puis réencodée en
/// WebP (le fichier d'origine n'est jamais stocké), rangée sous le préfixe public.
/// </summary>
public class CatalogImageService(StorageService storage)
{
    public const long MaxBytes = 5L * 1024 * 1024;
    public const int MaxSide = 1200;
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Renvoie la clé de l'image stockée.</summary>
    public async Task<string> StoreAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("Aucune image reçue.");
        if (file.Length > MaxBytes)
            throw new BusinessRuleException("Image trop volumineuse (5 Mo maximum).");
        if (!Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            throw new BusinessRuleException("Format accepté : JPEG, PNG ou WebP.");

        Image image;
        try
        {
            await using var input = file.OpenReadStream();
            image = await Image.LoadAsync(input, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new BusinessRuleException("Le fichier n'est pas une image valide.");
        }

        using (image)
        {
            if (image.Width > MaxSide || image.Height > MaxSide)
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(MaxSide, MaxSide), Mode = ResizeMode.Max }));
            using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, ct);

            var stored = await storage.UploadBytes(output.ToArray(), StorageService.CatalogFolder, ".webp", ct);
            return stored.Data ?? throw new BusinessConflictException(stored.Message);
        }
    }

    /// <summary>Après enregistrement de la nouvelle clé : au pire un fichier orphelin.</summary>
    public Task DeleteAsync(string? previousKey) =>
        StorageService.IsValidKey(previousKey) ? storage.Delete(previousKey!, CancellationToken.None) : Task.CompletedTask;

    public string? Url(string? key) => storage.Url(key);
}
