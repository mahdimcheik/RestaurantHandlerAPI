using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantHandler.Services;

/// <summary>
/// Images d'articles : décodées puis ré-encodées en webp avant le stockage public, ce qui écarte
/// tout fichier qui ne serait pas une vraie image et retire les métadonnées (EXIF, GPS).
/// </summary>
public class ArticleImageService(StorageService storage, ILogger<ArticleImageService> logger)
{
    public const long MAX_SIZE_BYTES = 10L * 1024 * 1024;
    private const int MAX_SOURCE_PIXELS = 10_000;
    private const int SECTION_MAX_WIDTH = 1600;
    private static readonly Size CoverSize = new(1200, 630);
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public async Task<Response<ArticleImage>> UploadAsync(IFormFile? file, bool cover, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Fail(400, "Aucun fichier reçu.");

        if (file.Length > MAX_SIZE_BYTES)
            return Fail(413, "Image trop volumineuse (maximum 10 Mo).");

        if (!Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return Fail(415, "Type de fichier non autorisé.");

        byte[] webp;
        try
        {
            await using var input = file.OpenReadStream();
            var info = await Image.IdentifyAsync(input, ct);
            if (info.Width > MAX_SOURCE_PIXELS || info.Height > MAX_SOURCE_PIXELS)
                return Fail(400, "Image trop grande.");

            input.Seek(0, SeekOrigin.Begin);
            using var image = await Image.LoadAsync(input, ct);
            image.Mutate(x =>
            {
                x.AutoOrient();
                if (cover)
                {
                    x.Resize(new ResizeOptions { Size = CoverSize, Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center });
                }
                else if (image.Width > SECTION_MAX_WIDTH)
                {
                    x.Resize(SECTION_MAX_WIDTH, 0);
                }
            });
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;

            using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, ct);
            webp = output.ToArray();
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            logger.LogInformation(ex, "Image d'article refusée");
            return Fail(415, "Le contenu du fichier n'est pas une image valide.");
        }

        var stored = await storage.UploadBytes(webp, StorageService.ArticleFolder, ".webp", ct);
        if (stored.Data is null)
            return Fail(stored.Status, stored.Message);

        return new Response<ArticleImage>
        {
            Status = 201,
            Message = "Image téléversée.",
            Data = new ArticleImage { Key = stored.Data, Url = storage.Url(stored.Data)! },
        };
    }

    private static Response<ArticleImage> Fail(int status, string message) => new() { Status = status, Message = message };
}
