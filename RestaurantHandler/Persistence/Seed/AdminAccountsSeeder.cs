using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;

namespace RestaurantHandler.Persistence.Seed;

/// <summary>
/// Compte Manager initial créé au démarrage s'il n'existe pas, à partir de <c>ADMIN_EMAIL</c>
/// et <c>ADMIN_PASSWORD</c>.
/// </summary>
public static class AdminAccountsSeeder
{
    public static void Seed(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserApp>>();

        var manager = new UserApp
        {
            UserName = EnvironmentVariables.ADMIN_EMAIL,
            Email = EnvironmentVariables.ADMIN_EMAIL,
            EmailConfirmed = true,
            StatusId = HardCode.ACCOUNT_ACTIVE,
            FirstName = "Manager",
            LastName = "Admin",
        };

        if (userManager.FindByEmailAsync(manager.Email).Result is not null)
            return;

        var created = userManager.CreateAsync(manager, EnvironmentVariables.ADMIN_PASSWORD).Result;
        if (created.Succeeded)
            userManager.AddToRoleAsync(manager, nameof(RoleEnum.Manager)).Wait();
    }
}
