using RestaurantHandler.Domain.Policies;

namespace RestaurantHandler.Tests.Policies;

public class BusinessIdentityPolicyTests
{
    [Theory]
    [InlineData("404833048")]
    [InlineData("732829320")]
    [InlineData("356000000")]
    public void Siren_a_cle_valide_accepte(string siren) => Assert.True(BusinessIdentityPolicy.IsValidSiren(siren));

    [Theory]
    [InlineData("404833049")] // clé fausse
    [InlineData("123456789")]
    [InlineData("40483304")] // 8 chiffres
    [InlineData("4048330480")] // 10 chiffres
    [InlineData("40483304A")]
    [InlineData("")]
    [InlineData(null)]
    public void Siren_invalide_refuse(string? siren) => Assert.False(BusinessIdentityPolicy.IsValidSiren(siren));

    [Fact]
    public void Siret_valide_et_rattache_au_siren()
    {
        Assert.True(BusinessIdentityPolicy.IsValidSiret("40483304800006", "404833048"));
        Assert.True(BusinessIdentityPolicy.IsValidSiret("40483304800014", "404833048"));
    }

    [Fact]
    public void Siret_d_un_autre_siren_ou_a_cle_fausse_refuse()
    {
        Assert.False(BusinessIdentityPolicy.IsValidSiret("73282932000009", "404833048"));
        Assert.False(BusinessIdentityPolicy.IsValidSiret("40483304800013", "404833048"));
        Assert.False(BusinessIdentityPolicy.IsValidSiret("4048330480000", "404833048"));
    }

    [Fact]
    public void Siret_de_la_poste_suit_la_somme_des_chiffres()
    {
        // Luhn faux, mais somme des chiffres multiple de 5 : accepté pour La Poste uniquement.
        Assert.True(BusinessIdentityPolicy.IsValidSiret("35600000000001", "356000000"));
        Assert.False(BusinessIdentityPolicy.IsValidSiret("35600000000002", "356000000"));
    }

    [Theory]
    [InlineData("404833048", "FR83404833048")]
    [InlineData("732829320", "FR44732829320")]
    public void Tva_intracommunautaire_calculee_depuis_le_siren(string siren, string vat) =>
        Assert.Equal(vat, BusinessIdentityPolicy.FrenchVatNumber(siren));

    [Fact]
    public void Normalisation_retire_separateurs_et_met_en_majuscules()
    {
        Assert.Equal("404833048", BusinessIdentityPolicy.Normalize(" 404 833 048 "));
        Assert.Equal("FR83404833048", BusinessIdentityPolicy.Normalize("fr 83-404.833.048"));
        Assert.Equal("", BusinessIdentityPolicy.Normalize(null));
    }

    [Fact]
    public void Controle_global()
    {
        Assert.Null(BusinessIdentityPolicy.Check("404833048", "40483304800006", "FR83404833048"));
        Assert.Null(BusinessIdentityPolicy.Check("404833048", null, null));
        Assert.Contains("SIREN", BusinessIdentityPolicy.Check("404833049", null, null));
        Assert.Contains("SIRET", BusinessIdentityPolicy.Check("404833048", "73282932000009", null));
        Assert.Contains("TVA", BusinessIdentityPolicy.Check("404833048", null, "FR44732829320"));
    }
}
