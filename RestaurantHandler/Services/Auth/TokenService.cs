using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace RestaurantHandler.Services;

public class TokenService
{
    private readonly MainContext _context;
    private readonly UserManager<UserApp> _userManager;
    private readonly StorageService _storage;

    public TokenService(MainContext context, UserManager<UserApp> userManager, StorageService storage)
    {
        _context = context;
        _userManager = userManager;
        _storage = storage;
    }

    /// <summary>
    /// Ouvre une session pour un utilisateur déjà authentifié (mot de passe vérifié ou ID
    /// token Google validé) : refresh token en base + cookie, puis JWT d'accès.
    ///
    /// Partagé par <see cref="Login"/> et <see cref="GoogleLogin"/> pour que les deux voies
    /// délivrent exactement le même contrat au front — en particulier le JWT produit par
    /// <see cref="GenerateAccessTokenAsync"/>, qui porte les claims Name/Email dont dépend
    /// <see cref="CheckUser.GetUserFromClaim"/>.
    /// </summary>
    /// <remarks>
    /// Le jeton de renouvellement est renvoyé dans <see cref="Login.RefreshToken"/> : c'est au
    /// contrôleur de le poser en cookie.
    /// </remarks>
    public async Task<Response<Login>> IssueSessionAsync(UserApp user)
    {
        // à la connection, je crée ou je met à jour le refreshtoken
        var refreshToken = await CreateOrUpdateTokenAsync(user, forceReset: true);

        await _context.SaveChangesAsync();
        var userRoles = await _userManager.GetRolesAsync(user);
        var roles = _context.Roles.ToList();

        var rolesDetailed = roles
            .Where(r => userRoles.Contains(r.Name ?? string.Empty))
            .Select(r => new RoleDetails(r))
            .ToList();

        var userDetails = new UserDetails(user, rolesDetailed);

        // L'URL signée est posée sur le DTO, pas sur l'entité : GenerateAccessTokenAsync
        // marque l'utilisateur Modified, un SaveChanges ultérieur écrirait sinon l'URL
        // complète dans ImgUrl, qui ne doit contenir que la clé d'objet.
        if (user.ImgUrl is not null)
        {
            userDetails.ImgUrl = _storage.Url(user.ImgUrl);
        }

        return new Response<Login>
        {
            Message = "Connexion réussite",
            Status = 200,
            Data = new Login
            {
                Token = await GenerateAccessTokenAsync(user),
                RefreshToken = refreshToken?.Token,
                User = userDetails,
                ProfileIncomplete = string.IsNullOrWhiteSpace(user.PhoneNumber),
            },
        };
    }

    private async Task<RefreshToken?> CreateOrUpdateTokenAsync(
        UserApp user,
        bool forceReset = false
    )
    {
        // à la connection, je crée ou je met à jour le refreshtoken
        var refreshToken = _context.RefreshTokens.FirstOrDefault(x => x.UserId == user.Id);

        if (refreshToken is null)
        {
            refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpirationDate = DateTimeOffset.UtcNow.AddDays(
                    EnvironmentVariables.COOKIES_VALIDITY_DAYS
                ),
            };
            _context.RefreshTokens.Add(refreshToken);
        }
        else if (forceReset)
        {
            refreshToken.UserId = user.Id;
            refreshToken.ExpirationDate = DateTimeOffset.UtcNow.AddDays(
                EnvironmentVariables.COOKIES_VALIDITY_DAYS
            );
        }

        await _context.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> RenewRefreshTokenAsync(UserApp user)
    {
        var refreshToken = _context.RefreshTokens.FirstOrDefault(x => x.UserId == user.Id);

        if (refreshToken is null)
        {
            _context.RefreshTokens.Add(
                new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    Token = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                    ExpirationDate = DateTimeOffset.UtcNow.AddDays(
                        EnvironmentVariables.COOKIES_VALIDITY_DAYS
                    ),
                }
            );
        }
        else
        {
            refreshToken.Token = Guid.NewGuid().ToString();
            refreshToken.UserId = user.Id;
            refreshToken.ExpirationDate = DateTimeOffset.UtcNow.AddDays(
                EnvironmentVariables.COOKIES_VALIDITY_DAYS
            );
        }

        await _context.SaveChangesAsync();

        return refreshToken;
    }

    /// <summary>
    /// Génère un token d'accès JWT pour l'utilisateur
    /// </summary>
    /// <param name="user">Utilisateur pour lequel générer le token</param>
    /// <returns>Token JWT en string</returns>
    public async Task<string> GenerateAccessTokenAsync(UserApp user)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(EnvironmentVariables.JWT_KEY)
        );
        var credentials = new SigningCredentials(
            key: securityKey,
            algorithm: SecurityAlgorithms.HmacSha256
        );

        var userRoles = await _userManager.GetRolesAsync(user);

        var authClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(type: ClaimTypes.Email, value: user.Email),
        };

        foreach (var userRole in userRoles)
        {
            authClaims.Add(new Claim(type: ClaimTypes.Role, value: userRole));
        }

        var token = new JwtSecurityToken(
            issuer: EnvironmentVariables.API_BACK_URL,
            audience: EnvironmentVariables.API_BACK_URL,
            claims: authClaims,
            expires: DateTime.UtcNow.AddMinutes(EnvironmentVariables.TOKEN_VALIDITY_MINUTES),
            signingCredentials: credentials
        );

        _context.Entry(user).State = EntityState.Modified;

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
