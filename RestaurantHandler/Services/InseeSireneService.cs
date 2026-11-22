using System.Net;
using System.Text.Json;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Vérifie un SIRET auprès de l'API Sirene de l'INSEE.
///
/// Authentification : le plan « Accès public » de Sirene est un plan API_KEY Gravitee — une
/// clé statique dans l'en-tête <see cref="EnvironmentVariables.INSEE_API_KEY_HEADER"/>, pas
/// d'OAuth2 ni d'échange de token.
///
/// Contrat : <see cref="VerifySiretAsync"/> ne lève JAMAIS. Elle est appelée depuis
/// <c>RegistrationService.Register</c> avant l'ouverture de la transaction, donc hors de tout
/// try/catch appelant — une exception qui s'échappe ferait planter l'inscription. Tout échec
/// (réseau, timeout, JSON, auth) devient <see cref="SiretVerificationApiStatus.ServiceUnavailable"/>,
/// que l'appelant traduit en « réessayez plus tard » (fail closed).
/// </summary>
public class InseeSireneService(HttpClient httpClient, ILogger<InseeSireneService> logger)
{
    public async Task<SiretVerificationResult> VerifySiretAsync(long siret)
    {
        if (string.IsNullOrWhiteSpace(EnvironmentVariables.INSEE_API_KEY))
        {
            return Unavailable(
                "INSEE_API_KEY non configurée — récupérer la clé dans la souscription de "
                    + "l'application à l'API Sirene sur portail-api.insee.fr",
                isError: true
            );
        }

        try
        {
            // Siret est stocké en long : un SIRET commençant par un zéro (des SIREN comme
            // 005520135 existent) perdrait ce zéro et produirait une URL à 13 chiffres.
            var request = new HttpRequestMessage(HttpMethod.Get, $"siret/{siret:D14}");
            request.Headers.Add(EnvironmentVariables.INSEE_API_KEY_HEADER, EnvironmentVariables.INSEE_API_KEY);

            using var response = await httpClient.SendAsync(request);

            return response.StatusCode switch
            {
                HttpStatusCode.OK => await ParseSuccessResponse(response),
                HttpStatusCode.NotFound => new SiretVerificationResult(SiretVerificationApiStatus.NotFound),

                // Le format/Luhn a déjà été validé par [Siret] en amont : un 400 ici est une
                // anomalie côté intégration, pas la preuve que le SIRET du prof est faux.
                HttpStatusCode.BadRequest => Unavailable("400 de l'API Sirene pour un SIRET pourtant bien formé"),

                HttpStatusCode.Unauthorized => Unavailable(
                    "401 de l'API Sirene — INSEE_API_KEY invalide (attention : le clientId/clientSecret "
                        + "de l'application n'est pas la clé d'API du plan)",
                    isError: true
                ),
                HttpStatusCode.Forbidden => Unavailable("403 de l'API Sirene — quota dépassé ou API non souscrite"),
                HttpStatusCode.TooManyRequests => Unavailable("429 de l'API Sirene — quota de 30 req/min dépassé"),
                _ => Unavailable($"HTTP {(int)response.StatusCode} inattendu de l'API Sirene"),
            };
        }
        catch (TaskCanceledException)
        {
            return Unavailable("Timeout de l'appel à l'API Sirene");
        }
        catch (HttpRequestException ex)
        {
            return Unavailable($"Erreur réseau vers l'API Sirene : {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur inattendue lors de la vérification du SIRET");
            return new SiretVerificationResult(SiretVerificationApiStatus.ServiceUnavailable);
        }
    }

    private async Task<SiretVerificationResult> ParseSuccessResponse(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var sireneResp = JsonSerializer.Deserialize<SireneResponse>(content, options);

            var etab = sireneResp?.Etablissement;
            if (etab?.UniteLegale is null)
            {
                return Unavailable("Réponse Sirene sans etablissement/uniteLegale exploitable");
            }

            // La période courante est celle qui n'est pas close (dateFin == null).
            var currentPeriod = etab.PeriodesEtablissement?.FirstOrDefault(p => p.DateFin is null);
            if (currentPeriod is null)
            {
                return Unavailable("Réponse Sirene sans période d'établissement courante");
            }

            // Un établissement ouvert peut appartenir à une unité légale cessée : les deux
            // doivent être actifs pour que le SIRET soit considéré comme valide.
            var isActive = currentPeriod.EtatAdministratifEtablissement == "A"
                && etab.UniteLegale.EtatAdministratifUniteLegale == "A";

            return new SiretVerificationResult(
                isActive ? SiretVerificationApiStatus.Active : SiretVerificationApiStatus.Closed,
                FormatCompanyName(etab.UniteLegale),
                FormatAddress(etab.AdresseEtablissement),
                FormatStreet(etab.AdresseEtablissement),
                etab.AdresseEtablissement?.LibelleCommuneEtablissement,
                etab.AdresseEtablissement?.CodePostalEtablissement,
                etab.ActivitePrincipaleEtablissement,
                etab.NomenclatureActivitePrincipaleEtablissement
            );
        }
        catch (JsonException ex)
        {
            return Unavailable($"Réponse Sirene illisible : {ex.Message}");
        }
    }

    /// <summary>Les entrepreneurs individuels n'ont pas de dénomination : on retombe sur prénom + nom.</summary>
    private static string? FormatCompanyName(SireneUniteLegale uniteLegale)
    {
        if (!string.IsNullOrWhiteSpace(uniteLegale.DenominationUniteLegale))
        {
            return uniteLegale.DenominationUniteLegale;
        }

        var name = $"{uniteLegale.Prenom1UniteLegale} {uniteLegale.NomUniteLegale}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? FormatAddress(SireneAdresse? addr)
    {
        if (addr is null)
        {
            return null;
        }

        var parts = new[]
        {
            addr.NumeroVoieEtablissement,
            addr.TypeVoieEtablissement,
            addr.LibelleVoieEtablissement,
            addr.CodePostalEtablissement,
            addr.LibelleCommuneEtablissement,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var address = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    /// <summary>Ligne "rue" seule (sans code postal/ville), pour peupler une <see cref="Address"/> structurée.</summary>
    private static string? FormatStreet(SireneAdresse? addr)
    {
        if (addr is null)
        {
            return null;
        }

        var parts = new[]
        {
            addr.NumeroVoieEtablissement,
            addr.TypeVoieEtablissement,
            addr.LibelleVoieEtablissement,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var street = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(street) ? null : street;
    }

    private SiretVerificationResult Unavailable(string message, bool isError = false)
    {
        if (isError)
        {
            logger.LogError(message);
        }
        else
        {
            logger.LogWarning(message);
        }

        return new SiretVerificationResult(SiretVerificationApiStatus.ServiceUnavailable);
    }
}
