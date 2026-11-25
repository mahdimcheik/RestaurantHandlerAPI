using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Options de décodage de toute image reçue de l'extérieur (upload, avatar Google).
/// ImageSharp choisit le décodeur d'après le contenu, pas l'extension : seuls JPEG, PNG, GIF et
/// WebP sont enregistrés, pour que les décodeurs TIFF, BMP, etc. (failles connues en 3.1.x,
/// version gardée car les suivantes exigent une licence au build) restent hors d'atteinte.
/// </summary>
public static class ImageDecoding
{
    public static readonly DecoderOptions Options = new()
    {
        Configuration = new Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new GifConfigurationModule(),
            new WebpConfigurationModule()
        ),
    };
}
