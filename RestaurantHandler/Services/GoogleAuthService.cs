using System.Text.Json;
using Google.Apis.Auth;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantHandler.Services;

/// <summary>
/// Connexion Google, côté vérification uniquement.
///
/// Le front (Google Identity Services) obtient un <b>ID token</b> et le poste sur
/// <c>POST /auth/google</c> ; ce service en vérifie la signature auprès de Google et
/// contrôle que l'audience est bien notre <see cref="EnvironmentVariables.GOOGLE_CLIENT_ID"/>.
/// On reste donc sur le pipeline JWT-only de l'API : pas de redirect OAuth, pas de
/// <c>client_secret</c>, aucun scheme d'authentification externe à brancher.
///
/// Contrat : <see cref="ValidateAsync"/> ne lève JAMAIS et est <b>fail-closed</b> —
/// tout token douteux renvoie <c>null</c>, l'appelant refuse la connexion.
/// </summary>
public class GoogleAuthService(
    HttpClient httpClient,
    StorageService storage,
    ILogger<GoogleAuthService> logger
)
{
    /// <summary>
    /// Valeur de <c>LoginProvider</c> dans <c>AspNetUserLogins</c> pour les comptes Google.
    /// </summary>
    public const string LoginProvider = "Google";

    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Valide l'ID token émis par Google et renvoie son payload, ou <c>null</c> si le token
    /// est invalide (signature, audience, issuer, expiration) ou si l'email n'est pas
    /// vérifié par Google. L'email vérifié est ce qui autorise le rattachement automatique
    /// à un compte existant : sans lui, n'importe qui pourrait revendiquer l'email d'autrui.
    /// </summary>
    public virtual async Task<GoogleJsonWebSignature.Payload?> ValidateAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(EnvironmentVariables.GOOGLE_CLIENT_ID))
        {
            logger.LogError("GOOGLE_CLIENT_ID n'est pas configuré : connexion Google refusée");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [EnvironmentVariables.GOOGLE_CLIENT_ID],
                }
            );

            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                logger.LogWarning("ID token Google valide mais sans email");
                return null;
            }

            if (!payload.EmailVerified)
            {
                logger.LogWarning("ID token Google refusé : email non vérifié par Google");
                return null;
            }

            return payload;
        }
        catch (InvalidJwtException ex)
        {
            // Le motif exact (audience, expiration, format) est ce qui distingue une erreur de
            // configuration d'un vrai token refusé : on le sort en clair plutôt que d'obliger
            // à décoder le JWT à la main.
            logger.LogWarning(
                "ID token Google invalide : {Reason}. Token présenté : {TokenClaims}. Audience attendue : {ExpectedAudience}",
                ex.Message,
                DescribeTokenUnsafe(idToken),
                EnvironmentVariables.GOOGLE_CLIENT_ID
            );
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Échec de la validation de l'ID token Google. Token présenté : {TokenClaims}. Audience attendue : {ExpectedAudience}",
                DescribeTokenUnsafe(idToken),
                EnvironmentVariables.GOOGLE_CLIENT_ID
            );
            return null;
        }
    }

    /// <summary>
    /// Décrit un ID token <b>sans le vérifier</b>, uniquement pour le diagnostic : quand Google
    /// rejette le token, savoir quelle audience a été présentée dit tout de suite s'il s'agit
    /// d'un mauvais <c>client_id</c>, d'un token expiré, ou d'autre chose qu'un ID token.
    ///
    /// <para>
    /// La valeur retournée n'est <b>jamais</b> utilisée pour une décision : le contenu n'est
    /// pas authentifié à ce stade. L'email est volontairement omis (donnée personnelle en clair
    /// dans les logs) ; <c>email_verified</c> suffit au diagnostic.
    /// </para>
    /// </summary>
    private static string DescribeTokenUnsafe(string idToken)
    {
        try
        {
            var segments = idToken.Split('.');
            if (segments.Length != 3)
            {
                return $"{segments.Length} segment(s) au lieu de 3 — ce n'est pas un JWT "
                    + "(un access token « ya29.… » au lieu de l'ID token ?)";
            }

            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = document.RootElement;

            string Claim(string name) =>
                root.TryGetProperty(name, out var value) ? value.ToString() : "absent";

            var expiry =
                root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                    ? $"{DateTimeOffset.FromUnixTimeSeconds(seconds):u} "
                        + $"({(DateTimeOffset.FromUnixTimeSeconds(seconds) < DateTimeOffset.UtcNow ? "EXPIRÉ" : "valide")})"
                    : "absent";

            return $"aud={Claim("aud")}, azp={Claim("azp")}, iss={Claim("iss")}, "
                + $"exp={expiry}, email_verified={Claim("email_verified")}";
        }
        catch
        {
            return "payload illisible";
        }
    }

    /// <summary>
    /// Récupère la photo de profil Google et la range comme un avatar téléversé
    /// (même traitement que <see cref="AvatarService.UploadAvatar"/> : carré 400x400, webp).
    /// Renvoie la <b>clé d'objet</b> à mettre dans <c>UserApp.ImgUrl</c> — surtout pas
    /// l'URL Google, puisque la lecture applique <see cref="StorageService.Url"/>.
    ///
    /// Best-effort : renvoie <c>null</c> sur n'importe quel échec, la création du compte
    /// ne doit pas dépendre d'un avatar.
    /// </summary>
    public virtual async Task<string?> TryFetchAvatarAsync(string? pictureUrl)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl))
            return null;

        try
        {
            using var response = await httpClient.GetAsync(pictureUrl);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HTTP {StatusCode} en récupérant l'avatar Google",
                    (int)response.StatusCode
                );
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxAvatarBytes)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0 || bytes.Length > MaxAvatarBytes)
                return null;

            using var image = Image.Load(bytes);
            image.Mutate(x =>
                x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center,
                    }
                )
            );

            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream);

            var stored = await storage.UploadBytes(
                outputStream.ToArray(),
                StorageService.AvatarFolder,
                ".webp",
                CancellationToken.None
            );

            return stored.Data;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de reprendre l'avatar Google");
            return null;
        }
    }
}
