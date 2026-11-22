namespace RestaurantHandler.Models;

/// <summary>Issue d'une vérification SIRET auprès de l'INSEE.</summary>
public enum SiretVerificationApiStatus
{
    Active, // établissement ET unité légale actifs → seul statut qui autorise l'inscription
    Closed, // trouvé, mais fermé/cessé → inscription refusée
    NotFound, // inconnu du répertoire Sirene → inscription refusée
    ServiceUnavailable, // INSEE injoignable / auth KO / quota → inscription refusée (fail closed)
}

public record SiretVerificationResult(
    SiretVerificationApiStatus Status,
    string? CompanyName = null,
    string? CompanyAddress = null,
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? ActivityCode = null,
    string? ActivityNomenclature = null
);

// DTOs de désérialisation de la réponse de l'API Sirene (sous-ensemble utilisé).
internal record SireneResponse(SireneEtablissement? Etablissement);

internal record SireneEtablissement(
    SireneUniteLegale? UniteLegale,
    SireneAdresse? AdresseEtablissement,
    List<SirenePeriodeEtablissement>? PeriodesEtablissement,
    string? ActivitePrincipaleEtablissement,
    string? NomenclatureActivitePrincipaleEtablissement
);

/// <summary>
/// L'état administratif de l'établissement n'est PAS à la racine de <c>etablissement</c> : Sirene
/// renvoie l'historique des périodes, la période courante étant celle dont <c>dateFin</c> est null.
/// </summary>
internal record SirenePeriodeEtablissement(
    string? DateFin,
    string? EtatAdministratifEtablissement
);

internal record SireneUniteLegale(
    string? EtatAdministratifUniteLegale,
    string? DenominationUniteLegale,
    string? NomUniteLegale,
    string? Prenom1UniteLegale
);

internal record SireneAdresse(
    string? NumeroVoieEtablissement,
    string? TypeVoieEtablissement,
    string? LibelleVoieEtablissement,
    string? CodePostalEtablissement,
    string? LibelleCommuneEtablissement
);
