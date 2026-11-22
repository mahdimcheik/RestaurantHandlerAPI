using RestaurantHandler.Utilities;

namespace RestaurantHandler.Tests;

public class EnvironmentVariablesTests
{
    private static Func<string, string?> From(Dictionary<string, string?> values) =>
        name => values.GetValueOrDefault(name);

    private static Dictionary<string, string?> AllRequired() =>
        EnvironmentVariables.RequiredAtStartup.ToDictionary(n => n, _ => (string?)"valeur");

    [Fact]
    public void Accepte_une_configuration_complete()
    {
        EnvironmentVariables.ValidateRequired(From(AllRequired()));
    }

    [Theory]
    [InlineData("JWT_KEY")]
    [InlineData("ADMIN_PASSWORD")]
    public void Refuse_de_demarrer_sans_un_secret_obligatoire(string missing)
    {
        var values = AllRequired();
        values[missing] = "  ";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentVariables.ValidateRequired(From(values))
        );
        Assert.Contains(missing, ex.Message);
    }
}
