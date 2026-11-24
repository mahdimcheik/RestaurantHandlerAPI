using RestaurantHandler.Services;

namespace RestaurantHandler.Tests;

public class CatalogImageKeysTests
{
    [Theory]
    [InlineData("catalog/2026/11/abc-w1200.webp", true)]
    [InlineData("catalog/2026/11/abc-w160.webp", true)]
    [InlineData("catalog/2026/11/abc.webp", false)]
    [InlineData(null, false)]
    public void Reconnait_une_cle_multi_tailles(string? key, bool expected) =>
        Assert.Equal(expected, CatalogImageService.HasVariants(key));

    [Fact]
    public void Derive_la_cle_d_une_autre_taille() =>
        Assert.Equal("catalog/2026/11/abc-w320.webp",
            CatalogImageService.VariantKey("catalog/2026/11/abc-w1200.webp", 320));
}
