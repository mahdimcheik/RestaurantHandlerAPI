namespace RestaurantHandler.Models.Filters;

public class FilterTeacher
{
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public string? FullName { get; set; }
    public string? CursusName { get; set; }
    public bool? InFavorites { get; set; }
    public List<Guid> CategoryIds { get; set; }
    public int First { get; set; }
    public int? Row { get; set; }
    public bool AvailableOnly { get; set; } = false;
    public double? Long { get; set; }
    public double? Lat { get; set; }
    public int Radius { get; set; } = 10;

    /// <summary>Bornes (incluses) du prix horaire d'au moins un cursus du prof.</summary>
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
}
