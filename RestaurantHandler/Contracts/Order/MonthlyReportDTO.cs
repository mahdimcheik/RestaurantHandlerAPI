using RestaurantHandler.Models.Interfaces;
using Stripe;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;
/// <summary>
/// Requête d'un bilan mensuel. Hérite de <see cref="CustomTableState"/> pour garder
/// la pagination / les filtres / la recherche des autres listes de commandes.
/// Le mois est celui du <b>paiement</b> (<c>Payment.CreatedAt</c>).
/// </summary>
public class MonthlyReportQuery : CustomTableState
{
    [Required]
    public int Year { get; set; }

    /// <summary>1 = janvier … 12 = décembre.</summary>
    [Required]
    public int Month { get; set; }
}
/// <summary>Une commande du bilan mensuel — vue ADMIN (comptabilité plateforme complète).</summary>
public class MonthlyReportRow
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>Date d'encaissement : c'est elle qui rattache la commande au mois.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    public string? StatusName { get; set; }
    public string? StudentName { get; set; }
    public string? TeacherName { get; set; }
    public int ReservationsCount { get; set; }

    [Required]
    public decimal GrossPaid { get; set; }

    /// <summary>Null tant que la BalanceTransaction Stripe n'est pas settled.</summary>
    public decimal? StripeFee { get; set; }
    public bool StripeFeePending { get; set; }

    [Required]
    public decimal NetCollected { get; set; }

    [Required]
    public decimal RefundedAmount { get; set; }

    [Required]
    public decimal TeacherOwed { get; set; }

    [Required]
    public decimal TeacherPaidOut { get; set; }

    [Required]
    public decimal TeacherTotal { get; set; }

    [Required]
    public decimal PlatformGain { get; set; }

    /// <summary>Gain provisoire : les frais Stripe ne sont pas encore connus.</summary>
    public bool PlatformGainProvisional { get; set; }

    /// <summary>Détail par réservation.</summary>
    public List<AccountingLine> Lines { get; set; } = [];
}
/// <summary>
/// Une commande du bilan mensuel — vue PROF. N'expose <b>aucune</b> donnée sensible :
/// ni frais Stripe, ni net encaissé, ni gain plateforme.
/// </summary>
public class TeacherMonthlyReportRow
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTimeOffset? PaidAt { get; set; }
    public string? StatusName { get; set; }
    public string? StudentName { get; set; }
    public int ReservationsCount { get; set; }

    /// <summary>Brut payé par l'élève.</summary>
    [Required]
    public decimal GrossPaid { get; set; }

    /// <summary>Commission plateforme retenue = <c>GrossPaid − RefundedAmount − TeacherTotal</c>.</summary>
    [Required]
    public decimal Commission { get; set; }

    [Required]
    public decimal RefundedAmount { get; set; }

    /// <summary>Crédité mais pas encore versé (<c>PayoutId == null</c>).</summary>
    [Required]
    public decimal TeacherOwed { get; set; }

    /// <summary>Déjà versé (<c>PayoutId != null</c>).</summary>
    [Required]
    public decimal TeacherPaidOut { get; set; }

    [Required]
    public decimal TeacherTotal { get; set; }

    public List<AccountingLine> Lines { get; set; } = [];
}
/// <summary>Totaux du mois — vue ADMIN. Calculés sur TOUT le mois filtré, pas sur la page.</summary>
public class MonthlyReportTotals
{
    public int OrdersCount { get; set; }
    public decimal GrossPaid { get; set; }

    /// <summary>Somme des frais Stripe <b>connus</b> (les non settled comptent pour 0).</summary>
    public decimal StripeFee { get; set; }

    /// <summary>Vrai si au moins une commande du mois n'a pas encore ses frais Stripe.</summary>
    public bool StripeFeePending { get; set; }

    public decimal NetCollected { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal TeacherOwed { get; set; }
    public decimal TeacherPaidOut { get; set; }
    public decimal TeacherTotal { get; set; }
    public decimal PlatformGain { get; set; }
    public bool PlatformGainProvisional { get; set; }
}
/// <summary>Totaux du mois — vue PROF.</summary>
public class TeacherMonthlyReportTotals
{
    public int OrdersCount { get; set; }
    public decimal GrossPaid { get; set; }
    public decimal Commission { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal TeacherOwed { get; set; }
    public decimal TeacherPaidOut { get; set; }
    public decimal TeacherTotal { get; set; }
}
/// <summary>
/// Bilan mensuel : les totaux portent sur le mois entier (filtres appliqués),
/// <see cref="Orders"/> ne contient que la page demandée.
/// </summary>
public class MonthlyReport<TRow, TTotals>
{
    [Required]
    public int Year { get; set; }

    [Required]
    public int Month { get; set; }

    /// <summary>Bornes UTC effectives de l'intervalle <c>[From, To[</c>.</summary>
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }

    public TTotals? Totals { get; set; }
    public List<TRow> Orders { get; set; } = [];
}
/// <summary>
/// Fichier renvoyé par les méthodes d'export. Le service retourne un
/// <c>Response&lt;CsvFile&gt;</c> : le contrôleur garde ainsi l'enveloppe JSON en cas
/// d'erreur et ne bascule sur <c>File(...)</c> qu'en cas de succès.
/// </summary>
public class CsvFile
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}
