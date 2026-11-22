using RestaurantHandler.Contexts;
using RestaurantHandler.Infrastructure.RateLimiting;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[Authorize]
[ApiController]
public class AuthController : ControllerBase
{
    #region Attributes

    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly RegistrationService registrationService;
    private readonly SignInService signInService;
    private readonly PasswordService passwordService;
    private readonly TokenService tokenService;
    private readonly AvatarService avatarService;

    public AuthController(
        MainContext context,
        UserManager<UserApp> userManager,
        RegistrationService registrationService,
        SignInService signInService,
        PasswordService passwordService,
        TokenService tokenService,
        AvatarService avatarService
    )
    {
        this._context = context;
        this._userManager = userManager;
        this.registrationService = registrationService;
        this.signInService = signInService;
        this.passwordService = passwordService;
        this.tokenService = tokenService;
        this.avatarService = avatarService;
    }

    #endregion

    #region Register update upload
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [EnableCors]
    [HttpPost("register")]
    public async Task<ActionResult<Response<UserDetails>>> Register([FromBody] UserCreate model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                new Response<object> { Status = 404, Message = "Problème de validation" }
            );
        }

        var response = await registrationService.Register(model);

        if (response.Status == 200 || response.Status == 201)
        {
            return Ok(response);
        }
        return BadRequest(response);
    }

    #endregion

    #region POST Login
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [Route("login")]
    [HttpPost]
    public async Task<ActionResult<Response<Login>>> Login([FromBody] UserLogin model)
    {
        if (!ModelState.IsValid)
            return BadRequest(
                new Response<object>
                {
                    Message = "Connexion échouée",
                    Status = 401,
                    Data = ModelState,
                }
            );
        var result = await signInService.Login(model);

        if (result.Status == 200 || result.Status == 201)
        {
            OpenSession(result.Data);
            return Ok(result);
        }
        return BadRequest(result);
    }

    #endregion

    #region POST Google login
    /// <summary>
    /// Connexion / inscription via Google. Le front envoie l'ID token (champ
    /// <c>credential</c>) obtenu de Google Identity Services ; la réponse est le même
    /// <see cref="Login"/> que <c>POST /auth/login</c>, cookie <c>refreshToken</c> compris.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [EnableCors]
    [Route("google")]
    [HttpPost]
    public async Task<ActionResult<Response<Login>>> GoogleLogin(
        [FromBody] GoogleLoginRequest model
    )
    {
        if (!ModelState.IsValid)
            return BadRequest(
                new Response<object>
                {
                    Message = "Connexion échouée",
                    Status = 401,
                    Data = ModelState,
                }
            );

        var result = await signInService.GoogleLogin(model);

        if (result.Status == 200 || result.Status == 201)
        {
            OpenSession(result.Data);
            return Ok(result);
        }
        return BadRequest(result);
    }

    #endregion

    #region Confirm account
    [AllowAnonymous]
    [Route("email-confirmation")]
    [HttpGet]
    public async Task<IActionResult> EmailConfirmation(
        [FromQuery] string userId,
        [FromQuery] string confirmationToken
    )
    {
        return Redirect(await registrationService.EmailConfirmation(userId, confirmationToken));
    }

    /// <summary>
    /// Renvoi d'un lien d'activation depuis la page « lien expiré ». La réponse ne dit pas si le
    /// compte existe ni s'il est déjà activé.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [Route("resend-confirmation")]
    [HttpPost]
    public async Task<ActionResult<Response<string?>>> ResendConfirmation(
        [FromBody] ResendConfirmationRequest model
    )
    {
        var result = await registrationService.ResendConfirmation(model.UserId);

        if (result == RegistrationService.ResendConfirmationResult.TooSoon)
        {
            return StatusCode(429, ResendTooSoon());
        }

        return Ok(
            new Response<string?>
            {
                Message = "Si ce compte attend une activation, un nouveau lien vient d'être envoyé",
                Status = 200,
            }
        );
    }

    /// <summary>Renvoi du lien d'activation pour l'utilisateur connecté (bandeau).</summary>
    [Route("me/resend-confirmation")]
    [HttpPost]
    public async Task<ActionResult<Response<string?>>> ResendMyConfirmation()
    {
        var user = CheckUser.GetUserFromClaim(HttpContext.User, _context);
        if (user is null)
        {
            return Unauthorized(
                new Response<string?> { Message = "Vous n'êtes pas connecté", Status = 401 }
            );
        }

        return await registrationService.ResendConfirmation(user.Id) switch
        {
            RegistrationService.ResendConfirmationResult.Sent => Ok(
                new Response<string?>
                {
                    Message = $"Un nouveau lien d'activation a été envoyé à {user.Email}",
                    Status = 200,
                }
            ),
            RegistrationService.ResendConfirmationResult.AlreadyConfirmed => Ok(
                new Response<string?> { Message = "Votre compte est déjà activé", Status = 200 }
            ),
            RegistrationService.ResendConfirmationResult.TooSoon => StatusCode(
                429,
                ResendTooSoon()
            ),
            _ => BadRequest(
                new Response<string?> { Message = "Renvoi impossible pour ce compte", Status = 400 }
            ),
        };
    }

    private static Response<string?> ResendTooSoon() =>
        new()
        {
            Message = "Un email d'activation vient d'être envoyé, réessayez dans quelques minutes",
            Status = 429,
        };

    #endregion

    /// <summary>Acceptation de la version courante des CGU par l'utilisateur connecté.</summary>
    [HttpPost("me/accept-terms")]
    public async Task<ActionResult<Response<object?>>> AcceptTerms()
    {
        var user = CheckUser.GetUserFromClaim(HttpContext.User, _context);
        if (user is null)
        {
            return Unauthorized(
                new Response<object?> { Message = "Vous n'êtes pas connecté", Status = 401 }
            );
        }

        user.AcceptTerms();
        await _context.SaveChangesAsync();

        return Ok(
            new Response<object?>
            {
                Message = "Conditions générales d'utilisation acceptées",
                Status = 200,
            }
        );
    }

    /// <summary>Enregistre les préférences visuelles de l'utilisateur connecté.</summary>
    [HttpPut("me/layout-settings")]
    public async Task<ActionResult<Response<LayoutSettingsDetails>>> UpdateLayoutSettings(
        [FromBody] LayoutSettingsUpdate settings
    )
    {
        var user = CheckUser.GetUserFromClaim(HttpContext.User, _context);
        if (user is null)
        {
            return Unauthorized(
                new Response<LayoutSettingsDetails> { Message = "Vous n'êtes pas connecté", Status = 401 }
            );
        }

        user.LayoutSettings = settings.ToEntity();
        await _context.SaveChangesAsync();

        return Ok(
            new Response<LayoutSettingsDetails>
            {
                Message = "Préférences visuelles enregistrées",
                Status = 200,
                Data = new LayoutSettingsDetails(user.LayoutSettings),
            }
        );
    }

    #region CurrentUser informations
    [HttpGet("my-informations")]
    public async Task<ActionResult<Response<UserInfosWithtoken>>> GetMyInformations()
    {
        var user = CheckUser.GetUserFromClaim(HttpContext.User, _context);

        if (user == null)
            return BadRequest(
                new Response<UserInfosWithtoken>
                {
                    Message = "Vous n'êtes pas connecté",
                    Status = 401,
                }
            );

        var userRoles = await _userManager.GetRolesAsync(user);
        var roles = _context.Roles.ToList();

        var rolesDetailed = roles
            .Where(r => userRoles.Contains(r.Name ?? string.Empty))
            .Select(r => new RoleDetails(r))
            .ToList();

        return Ok(
            new Response<UserInfosWithtoken>
            {
                Message = "Demande acceptée",
                Status = 200,
                Data = new UserInfosWithtoken
                {
                    Token = await tokenService.GenerateAccessTokenAsync(user),
                    User = new UserDetails(user, rolesDetailed).WithLayoutSettings(user),
                },
            }
        );
    }
    #endregion

    #region POST AskForPasswordRecoveryMail
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [Route("forgot-password")]
    [HttpPost]
    public async Task<ActionResult<Response<PasswordReset?>>> ForgotPassword(
        [FromBody] ForgotPassword model
    )
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                new Response<PasswordReset?> { Message = "Demande refusée", Status = 400 }
            );
        }

        var result = await passwordService.ForgotPassword(model);

        if (result.Status == 200 || result.Status == 201)
        {
            return Ok(result);
        }
        return BadRequest(result);
    }

    #endregion

    #region PasswordChange after recovery
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [Route("reset-password")]
    [HttpPost]
    public async Task<ActionResult<Response<string?>>> ChangePassword(
        [FromBody] PasswordRecovery model
    )
    {
        if (!ModelState.IsValid || model.Password != model.PasswordConfirmation)
        {
            return BadRequest(new Response<string?> { Message = "Demande refusée", Status = 400 });
        }

        var result = await passwordService.ChangePassword(model);

        if (result.Status == 200 || result.Status == 201)
        {
            return Ok(result.Message);
        }
        return BadRequest(result);
    }

    #endregion

    #region refresh token
    [Route("refresh-token")]
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<Response<Login?>>> UpdateRefreshToken()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
        {
            return Unauthorized(
                new Response<Login?> { Message = "Refresh token non-existant", Status = 401 }
            );
        }

        var result = await signInService.UpdateRefreshToken(refreshToken);

        if (result.Status == 200 || result.Status == 201)
        {
            AllowCredentials();
            return Ok(result);
        }
        return BadRequest(result);
    }

    #endregion

    #region logout
    [AllowAnonymous]
    [HttpGet("logout")]
    public async Task<ActionResult<Response<object?>>> Logout()
    {
        // Récupération de l'email/nom d'utilisateur actuel pour nettoyer les connexions
        var userEmail =
            HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? HttpContext.User?.Identity?.Name;

        Response.Cookies.Delete("refreshToken");

        return Ok(new Response<object> { Message = "Vous êtes déconnecté", Status = 200 });
    }
    #endregion

    #region avatar
    [HttpPost("upload-avatar")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    public async Task<ActionResult<Response<FileUrl>?>> OnPostUploadAsync(IFormFile file)
    {
        var result = await avatarService.UploadAvatar(file, await this.GetCallerAsync());

        return StatusCode(result.Status, result);
    }

    #endregion

    /// <summary>Le front envoie ses requêtes avec identifiants : sans cet en-tête, le cookie est ignoré.</summary>
    private void AllowCredentials() =>
        Response.Headers.Append(key: "Access-Control-Allow-Credentials", value: "true");

    private void OpenSession(Login? session)
    {
        AllowCredentials();
        if (session?.RefreshToken is null)
            return;

        Response.Cookies.Append(
            "refreshToken",
            session.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = EnvironmentVariables.COOKIES_SECURE,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(EnvironmentVariables.COOKIES_VALIDITY_DAYS),
            }
        );
    }
}
