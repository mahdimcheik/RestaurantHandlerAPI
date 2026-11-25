using RestaurantHandler.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RestaurantHandler.Tests;

public class ImageDecodingTests
{
    private static byte[] Encode(Action<Image, Stream> save)
    {
        using var image = new Image<Rgba32>(4, 4);
        using var stream = new MemoryStream();
        save(image, stream);
        return stream.ToArray();
    }

    [Fact]
    public void Accepte_les_formats_de_la_carte()
    {
        foreach (var bytes in new[]
        {
            Encode((i, s) => i.SaveAsJpeg(s)),
            Encode((i, s) => i.SaveAsPng(s)),
            Encode((i, s) => i.SaveAsGif(s)),
            Encode((i, s) => i.SaveAsWebp(s)),
        })
        {
            using var image = Image.Load(ImageDecoding.Options, bytes);
            Assert.Equal(4, image.Width);
        }
    }

    [Fact]
    public void Refuse_un_tiff_meme_renomme()
    {
        var tiff = Encode((i, s) => i.SaveAsTiff(s));
        Assert.Throws<UnknownImageFormatException>(() => Image.Load(ImageDecoding.Options, tiff));
    }
}
