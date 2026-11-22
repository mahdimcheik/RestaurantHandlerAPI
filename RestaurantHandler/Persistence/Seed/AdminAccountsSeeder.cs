using RestaurantHandler.Contexts;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;

namespace RestaurantHandler.Persistence.Seed;

/// <summary>
/// Comptes d'administration créés au démarrage s'ils n'existent pas, à partir de
/// <c>SUPER_ADMIN_*</c> et <c>ADMIN_*</c>.
/// </summary>
public static class AdminAccountsSeeder
{
    public static void Seed(IServiceProvider serviceProvider)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserApp>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<RoleApp>>();

            // Seed a default super admin user
            var superAdminEmail = new UserApp
            {
                UserName = EnvironmentVariables.SUPER_ADMIN_EMAIL,
                Email = EnvironmentVariables.SUPER_ADMIN_EMAIL,
                EmailConfirmed = true,
                StatusId = HardCode.ACCOUNT_ACTIVE,
                GenderId = HardCode.GENDER_OTHER,
                FirstName = "Super",
                LastName = "Admin",
                DateOfBirth = DateTimeOffset.UtcNow.AddYears((-20)),
            };
            var superAdminPassword = EnvironmentVariables.SUPER_ADMIN_PASSWORD;
            if (userManager.FindByEmailAsync(superAdminEmail.Email).Result == null)
            {
                var createPowerUser = userManager
                    .CreateAsync(superAdminEmail, superAdminPassword)
                    .Result;
                if (createPowerUser.Succeeded)
                {
                    if (!roleManager.RoleExistsAsync("SuperAdmin").Result)
                    {
                        var role = new RoleApp { Name = "SuperAdmin" };
                        roleManager.CreateAsync(role).Wait();
                    }
                    userManager.AddToRoleAsync(superAdminEmail, "SuperAdmin").Wait();
                }
            }

            var adminEmail = new UserApp
            {
                UserName = EnvironmentVariables.ADMIN_EMAIL,
                Email = EnvironmentVariables.ADMIN_EMAIL,
                EmailConfirmed = true,
                StatusId = HardCode.ACCOUNT_ACTIVE,
                GenderId = HardCode.GENDER_OTHER,
                FirstName = "Simple",
                LastName = "Admin",
                DateOfBirth = DateTimeOffset.UtcNow.AddYears((-20)),
            };

            var adminPassword = EnvironmentVariables.ADMIN_PASSWORD;
            if (userManager.FindByEmailAsync(adminEmail.Email).Result == null)
            {
                var createPowerUser = userManager.CreateAsync(adminEmail, adminPassword).Result;
                if (createPowerUser.Succeeded)
                {
                    if (!roleManager.RoleExistsAsync("Admin").Result)
                    {
                        var role = new RoleApp { Name = "Admin" };
                        roleManager.CreateAsync(role).Wait();
                    }
                    userManager.AddToRoleAsync(adminEmail, "Admin").Wait();
                }
            }
        }
    }
}
