using System.Web;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;

namespace RestaurantHandler.Services;

public class PasswordService
{
    private readonly UserManager<UserApp> _userManager;
    private readonly MailService _mailService;
    private readonly TokenService _tokenService;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(
        UserManager<UserApp> userManager,
        MailService mailService,
        TokenService tokenService,
        ILogger<PasswordService> logger
    )
    {
        _userManager = userManager;
        _mailService = mailService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Même réponse que l'adresse existe ou non, et le jeton ne quitte jamais le serveur autrement
    /// que par l'e-mail : le renvoyer ici permettait de réinitialiser n'importe quel compte.
    /// </summary>
    public async Task<Response<PasswordReset>> ForgotPassword(ForgotPassword model)
    {
        var response = new Response<PasswordReset>
        {
            Message =
                "Si un compte correspond à cette adresse, un email de réinitialisation vient d'y être envoyé",
            Status = 200,
            Data = null,
        };

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            try
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                resetToken = HttpUtility.UrlEncode(resetToken);

                var resetLink =
                    EnvironmentVariables.API_FRONT_URL
                    + "/auth/reset-password?userId="
                    + user.Id
                    + "&resetToken="
                    + resetToken;

                // Tentative d'envoi de l'e-mail pour la regénération du mot de passe

                await _mailService.SendResetPasswordAccount(user, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de l'envoi du mail de réinitialisation à {UserId}", user.Id);
            }
        }

        return response;
    }

    public async Task<Response<string?>> ChangePassword(PasswordRecovery model)
    {
        var invalid = new Response<string?>
        {
            Message = "Problème de validation, votre token est valid ?",
            Status = 404,
        };

        UserApp? user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            return invalid;
        }

        IdentityResult result = await _userManager.ResetPasswordAsync(
            user: user,
            token: model.ResetToken,
            newPassword: model.Password
        );

        if (result.Succeeded)
        {
            await _tokenService.RenewRefreshTokenAsync(user);
            return new Response<string?>
            {
                Message = $"{EnvironmentVariables.API_FRONT_URL}/auth/password-reset-success", //  "Mot de passe vient d'être modifié",
                Status = 201,
            };
        }

        return invalid;
    }
}
