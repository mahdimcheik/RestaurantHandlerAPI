using RestaurantHandler.Models;

namespace RestaurantHandler.Tests;

public class UserUpdateTests
{
    private static UserApp NewUser() => new() { FirstName = "A", LastName = "B", PhoneNumber = "0600000000" };

    [Fact]
    public void Enregistre_le_telephone_sans_espaces_autour()
    {
        var user = NewUser();

        new UserUpdate { FirstName = "Léa", LastName = "Martin", PhoneNumber = " 0612345678 " }.UpdateUser(user);

        Assert.Equal("Léa", user.FirstName);
        Assert.Equal("0612345678", user.PhoneNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_telephone_vide_efface_le_numero(string? phone)
    {
        var user = NewUser();

        new UserUpdate { FirstName = "Léa", LastName = "Martin", PhoneNumber = phone }.UpdateUser(user);

        Assert.Null(user.PhoneNumber);
    }
}
