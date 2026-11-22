using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models;

public enum StatsPeriod
{
    Day,

    /// <summary>Du lundi au dimanche contenant <see cref="StatsPeriodQuery.From"/>.</summary>
    Week,
    Month,

    /// <summary>Du <see cref="StatsPeriodQuery.From"/> au <see cref="StatsPeriodQuery.To"/> inclus.</summary>
    Custom,
}

public class StatsPeriodQuery
{
    public StatsPeriod Period { get; set; } = StatsPeriod.Day;

    /// <summary>Jour de référence (heure de Paris), aujourd'hui par défaut ; début de la plage libre.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Fin incluse de la plage libre.</summary>
    public DateOnly? To { get; set; }
}

public class PeakHoursQuery : StatsPeriodQuery
{
    [AllowedValues(15, 30, ErrorMessage = "Tranche de 15 ou 30 minutes.")]
    public int BucketMinutes { get; set; } = 30;
}

// ---------- US-E09-1 : tableau du jour ----------

public class ChannelStats
{
    [Required] public int Orders { get; set; }
    [Required] public int RevenueCents { get; set; }
}

public class DashboardStats
{
    [Required] public DateOnly Date { get; set; }
    [Required] public int RevenueCents { get; set; }
    [Required] public int Orders { get; set; }
    [Required] public int AverageBasketCents { get; set; }
    [Required] public ChannelStats Online { get; set; } = new();
    [Required] public ChannelStats Counter { get; set; } = new();

    /// <summary>Paiements SumUp réussis des commandes comptées.</summary>
    [Required] public int PaidOnlineCents { get; set; }

    /// <summary>Encaissements déclarés sur place des commandes comptées.</summary>
    [Required] public int PaidOnSiteCents { get; set; }
    [Required] public List<VatLineDto> Vat { get; set; } = [];

    /// <summary>Commandes du jour de service par statut (tous statuts, y compris 0).</summary>
    [Required] public Dictionary<OrderStatus, int> ByStatus { get; set; } = [];
}

// ---------- US-E09-2 : ventes par produit ----------

public class ProductSales
{
    [Required] public Guid ProductId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;

    /// <summary>Vendu seul (hors menu).</summary>
    [Required] public int QuantityAlone { get; set; }

    /// <summary>Choisi comme composant d'un menu.</summary>
    [Required] public int QuantityInMenus { get; set; }

    /// <summary>CA des ventes seules (personnalisations comprises) ; le CA des menus est porté par le menu.</summary>
    [Required] public int RevenueCents { get; set; }
}

public class MenuSales
{
    [Required] public Guid MenuId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int Quantity { get; set; }
    [Required] public int RevenueCents { get; set; }
}

public class SupplementSales
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public int Quantity { get; set; }
    [Required] public int RevenueCents { get; set; }
}

public class ProductSalesReport
{
    [Required] public DateOnly From { get; set; }
    [Required] public DateOnly To { get; set; }
    [Required] public List<ProductSales> Products { get; set; } = [];
    [Required] public List<MenuSales> Menus { get; set; } = [];
    [Required] public List<SupplementSales> Supplements { get; set; } = [];
}

// ---------- US-E09-3 : heures de pointe ----------

public class PeakHoursBucket
{
    [Required] public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Début de la tranche, heure de Paris.</summary>
    [Required] public TimeOnly Start { get; set; }
    [Required] public int Orders { get; set; }
    [Required] public int RevenueCents { get; set; }

    /// <summary>Temps moyen entre `Confirmée` et `Prête`, en minutes ; nul si aucune mesure.</summary>
    public double? AveragePreparationMinutes { get; set; }
}

public class PeakHoursReport
{
    [Required] public DateOnly From { get; set; }
    [Required] public DateOnly To { get; set; }
    [Required] public int BucketMinutes { get; set; }

    /// <summary>Seules les tranches ayant au moins une commande, par jour (lundi d'abord) puis heure.</summary>
    [Required] public List<PeakHoursBucket> Buckets { get; set; } = [];
}
