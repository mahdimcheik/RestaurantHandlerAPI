using System.Text.RegularExpressions;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantHandler.Services;

/// <summary>
/// Photos de la carte et logo : formats et taille contrôlés, image redécodée puis réencodée en
/// WebP (le fichier d'origine n'est jamais stocké), rangée sous le préfixe public.
/// Chaque photo est déclinée en plusieurs tailles rangées côte à côte :
/// <c>catalog/yyyy/MM/{guid}-w{taille}.webp</c>. La clé persistée est celle de la plus grande ;
/// le client en déduit les autres en remplaçant le suffixe (srcset).
/// </summary>
public partial class CatalogImageService(StorageService storage)
{
    public const long MaxBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Plus grand côté de chaque variante, croissant. Le client Angular a la même liste
    /// (<c>catalog-image-loader.ts</c>).
    /// </summary>
    public static readonly int[] Widths = [160, 320, 640, 1200];

    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    [GeneratedRegex(@"-w\d+\.webp$")]
    private static partial Regex VariantSuffix();

    /// <summary>Clé de la variante <paramref name="width"/> d'une photo multi-tailles.</summary>
    public static string VariantKey(string key, int width) => VariantSuffix().Replace(key, $"-w{width}.webp");

    /// <summary>Vrai pour une clé multi-tailles ; les photos envoyées avant n'ont qu'une version.</summary>
    public static bool HasVariants(string? key) => key is not null && VariantSuffix().IsMatch(key);

    /// <summary>Renvoie la clé de l'image stockée (celle de la plus grande variante).</summary>
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
            image = await Image.LoadAsync(ImageDecoding.Options, input, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new BusinessRuleException("Le fichier n'est pas une image valide.");
        }

        var stem = StorageService.BuildKey(StorageService.CatalogFolder, ".webp")[..^".webp".Length];
        var stored = new List<string>(Widths.Length);

        using (image)
        {
            image.Mutate(x => x.AutoOrient());
            try
            {
                foreach (var width in Widths)
                {
                    var key = $"{stem}-w{width}.webp";
                    var result = await storage.UploadBytesAt(await EncodeAsync(image, width, ct), key, ct);
                    if (result.Data is null)
                        throw new BusinessConflictException(result.Message);
                    stored.Add(key);
                }
            }
            catch
            {
                foreach (var key in stored)
                    await storage.Delete(key, CancellationToken.None);
                throw;
            }
        }

        return stored[^1];
    }

    /// <summary>Jamais agrandie : une photo plus petite que la variante garde sa taille d'origine.</summary>
    private static async Task<byte[]> EncodeAsync(Image image, int side, CancellationToken ct)
    {
        using var resized = image.Width > side || image.Height > side
            ? image.Clone(x => x.Resize(new ResizeOptions { Size = new Size(side, side), Mode = ResizeMode.Max }))
            : image.Clone(_ => { });
        using var output = new MemoryStream();
        await resized.SaveAsWebpAsync(output, new WebpEncoder { Quality = side <= 320 ? 80 : 82 }, ct);
        return output.ToArray();
    }

    /// <summary>Après enregistrement de la nouvelle clé : au pire des fichiers orphelins.</summary>
    public async Task DeleteAsync(string? previousKey)
    {
        if (!StorageService.IsValidKey(previousKey))
            return;

        if (!HasVariants(previousKey))
        {
            await storage.Delete(previousKey!, CancellationToken.None);
            return;
        }

        foreach (var width in Widths)
            await storage.Delete(VariantKey(previousKey!, width), CancellationToken.None);
    }

    public string? Url(string? key) => storage.Url(key);
}
