using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Horaires, fermetures, pause et paramètres (E03, US-E10-5). Horaires et fermetures sont mis en
/// cache ; la pause et les paramètres sont toujours relus en base (ADR-0004).
/// </summary>
public class RestaurantService(
    MainContext db,
    HybridCache cache,
    CatalogChangeNotifier changes,
    CatalogImageService images,
    IBackgroundJobClient jobs,
    TimeProvider clock
)
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record Schedule(List<OpeningHours> Hours, List<Closure> Closures);

    // ---------- Lecture publique ----------

    public async Task<OpeningStateDto> GetOpeningStateAsync()
    {
        var schedule = await GetScheduleAsync();
        var settings = await ReadSettingsAsync();
        var state = OpeningPolicy.Compute(clock.GetUtcNow(), schedule.Hours, schedule.Closures, settings.IsPaused, settings.PausedUntil);

        return new OpeningStateDto
        {
            Status = state.Status,
            ClosesAt = state.ClosesAt,
            NextOpeningAt = state.NextOpeningAt,
            PausedUntil = state.PausedUntil,
            ClosureReason = state.ClosureReason,
            AsapDelayMinutes = settings.AsapDelayMinutes,
        };
    }

    /// <summary>Créneaux du jour, places restantes comprises ; aucun pendant une pause (R7).</summary>
    public async Task<List<PickupSlotDto>> GetSlotsAsync()
    {
        var now = clock.GetUtcNow();
        var settings = await ReadSettingsAsync();
        if (OpeningPolicy.IsPauseActive(settings.IsPaused, settings.PausedUntil, now))
            return [];

        return (await SlotsWithOccupancyAsync(now, await GetScheduleAsync(), settings))
            .Select(s => new PickupSlotDto { Start = s.Start, End = s.End, Remaining = s.Remaining })
            .ToList();
    }

    /// <summary>
    /// Créneaux non pleins. Toute commande active dont l'heure de retrait tombe dans un créneau
    /// l'occupe, quel que soit son canal : la capacité protège la cuisine. Jamais en cache.
    /// </summary>
    public async Task<List<PickupSlot>> SlotsWithOccupancyAsync(DateTimeOffset now, Schedule schedule, RestaurantSettings settings)
    {
        var candidates = SlotPolicy.ForToday(now, schedule.Hours, schedule.Closures, settings.SlotDurationMinutes, settings.SlotCapacityOrders, settings.MinLeadMinutes);
        if (candidates.Count == 0)
            return [];

        var from = candidates[0].Start;
        var to = candidates[^1].End;
        var pickups = await db.Orders.AsNoTracking()
            .Where(o => o.PickupAt >= from && o.PickupAt < to && OrderTransitionPolicy.ActiveStatuses.Contains(o.Status))
            .Select(o => o.PickupAt)
            .ToListAsync();

        var occupancy = candidates.ToDictionary(s => s.Start, s => pickups.Count(p => p >= s.Start && p < s.End));
        return SlotPolicy
            .ForToday(now, schedule.Hours, schedule.Closures, settings.SlotDurationMinutes, settings.SlotCapacityOrders, settings.MinLeadMinutes, occupancy)
            .ToList();
    }

    public async Task<RestaurantInfo> GetInfoAsync()
    {
        var settings = await ReadSettingsAsync();
        var schedule = await GetScheduleAsync();
        var nowLocal = RestaurantClock.ToLocal(clock.GetUtcNow());

        return new RestaurantInfo
        {
            TradeName = settings.TradeName,
            LegalName = settings.LegalName,
            Address = settings.Address,
            Phone = settings.Phone,
            Siret = settings.Siret,
            VatNumber = settings.VatNumber,
            LogoUrl = images.Url(settings.LogoUrl),
            Hours = schedule.Hours.Select(ToDto).ToList(),
            UpcomingClosures = schedule.Closures.Where(c => c.EndLocal > nowLocal).Select(ToDto).ToList(),
        };
    }

    // ---------- Horaires ----------

    public async Task<List<OpeningRangeDto>> GetHoursAsync() => (await GetScheduleAsync()).Hours.Select(ToDto).ToList();

    public async Task<List<OpeningRangeDto>> ReplaceHoursAsync(WeekHoursUpdate dto)
    {
        var ranges = dto.Ranges.Select(r => new WeeklyRange(r.DayOfWeek, r.Start, r.End)).ToList();
        if (OpeningHoursPolicy.Validate(ranges) is { } error)
            throw new BusinessRuleException(error);

        db.OpeningHours.RemoveRange(await db.OpeningHours.ToListAsync());
        db.OpeningHours.AddRange(ranges.Select(r => new OpeningHours { DayOfWeek = r.DayOfWeek, Start = r.Start, End = r.End }));
        await db.SaveChangesAsync();
        await changes.ScheduleChangedAsync();

        return await GetHoursAsync();
    }

    // ---------- Fermetures ----------

    public async Task<List<ClosureDetails>> ListClosuresAsync(bool includePast)
    {
        var nowLocal = RestaurantClock.ToLocal(clock.GetUtcNow());
        return await db.Closures.AsNoTracking()
            .Where(c => includePast || c.EndLocal > nowLocal)
            .OrderBy(c => c.StartLocal)
            .Select(c => new ClosureDetails { Id = c.Id, StartLocal = c.StartLocal, EndLocal = c.EndLocal, Reason = c.Reason })
            .ToListAsync();
    }

    public async Task<ClosureDetails> CreateClosureAsync(ClosureWrite dto)
    {
        var closure = new Closure
        {
            StartLocal = DateTime.SpecifyKind(dto.StartLocal, DateTimeKind.Unspecified),
            EndLocal = DateTime.SpecifyKind(dto.EndLocal, DateTimeKind.Unspecified),
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
        };
        db.Closures.Add(closure);
        await db.SaveChangesAsync();
        await changes.ScheduleChangedAsync();

        return ToDto(closure);
    }

    public async Task DeleteClosureAsync(Guid id)
    {
        var closure = await db.Closures.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new ResourceNotFoundException("Fermeture introuvable.");
        db.Closures.Remove(closure);
        await db.SaveChangesAsync();
        await changes.ScheduleChangedAsync();
    }

    // ---------- Pause ----------

    public async Task<OpeningStateDto> PauseAsync(PauseRequest dto)
    {
        var settings = await LoadSettingsAsync();
        settings.IsPaused = true;
        settings.PausedUntil = dto.DurationMinutes is { } minutes ? clock.GetUtcNow().AddMinutes(minutes) : null;
        await db.SaveChangesAsync();

        if (settings.PausedUntil is { } until)
            jobs.Schedule<RestaurantService>(s => s.EndExpiredPauseAsync(), until);

        await changes.OpeningChangedAsync();
        return await GetOpeningStateAsync();
    }

    public async Task<OpeningStateDto> ResumeAsync()
    {
        var settings = await LoadSettingsAsync();
        settings.IsPaused = false;
        settings.PausedUntil = null;
        await db.SaveChangesAsync();
        await changes.OpeningChangedAsync();

        return await GetOpeningStateAsync();
    }

    /// <summary>
    /// Tâche Hangfire planifiée à la fin d'une pause : sans effet si la pause a été levée ou
    /// prolongée entre-temps, sinon la lève et prévient les clients.
    /// </summary>
    public async Task EndExpiredPauseAsync()
    {
        var settings = await LoadSettingsAsync();
        if (!settings.IsPaused || settings.PausedUntil is null || settings.PausedUntil > clock.GetUtcNow())
            return;

        settings.IsPaused = false;
        settings.PausedUntil = null;
        await db.SaveChangesAsync();
        await changes.OpeningChangedAsync();
    }

    // ---------- Paramètres ----------

    public async Task<RestaurantSettingsDetails> GetSettingsAsync() => ToDetails(await ReadSettingsAsync());

    public async Task<RestaurantSettingsDetails> UpdateSettingsAsync(RestaurantSettingsWrite dto, CallerIdentity caller)
    {
        var settings = await LoadSettingsAsync();
        settings.TradeName = dto.TradeName.Trim();
        settings.LegalName = Clean(dto.LegalName);
        settings.Address = Clean(dto.Address);
        settings.Phone = Clean(dto.Phone);
        settings.Siret = Clean(dto.Siret)?.Replace(" ", "");
        settings.VatNumber = Clean(dto.VatNumber)?.ToUpperInvariant();
        settings.AsapDelayMinutes = dto.AsapDelayMinutes;
        settings.SlotDurationMinutes = dto.SlotDurationMinutes;
        settings.SlotCapacityOrders = dto.SlotCapacityOrders;
        settings.MinLeadMinutes = dto.MinLeadMinutes;
        settings.ApprovalTimeoutMinutes = dto.ApprovalTimeoutMinutes;
        settings.PickupGraceMinutes = dto.PickupGraceMinutes;
        settings.NoShowThreshold = dto.NoShowThreshold;
        settings.OnlinePaymentExpiryMinutes = dto.OnlinePaymentExpiryMinutes;

        await SaveWithHistoryAsync(settings, caller);
        await changes.OpeningChangedAsync();
        return ToDetails(settings);
    }

    public async Task<RestaurantSettingsDetails> SetLogoAsync(IFormFile file, CallerIdentity caller, CancellationToken ct)
    {
        var settings = await LoadSettingsAsync();
        var key = await images.StoreAsync(file, ct);
        var previous = settings.LogoUrl;

        settings.LogoUrl = key;
        await SaveWithHistoryAsync(settings, caller);
        await images.DeleteAsync(previous);
        return ToDetails(settings);
    }

    public async Task<List<RestaurantSettingsHistoryItem>> GetSettingsHistoryAsync(int take) =>
        await db.RestaurantSettingsHistory.AsNoTracking()
            .OrderByDescending(h => h.ChangedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(h => new RestaurantSettingsHistoryItem
            {
                Id = h.Id,
                ChangedAt = h.ChangedAt,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByName = db.Users.Where(u => u.Id == h.ChangedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                Snapshot = h.SnapshotJson,
            })
            .ToListAsync();

    // ---------- Interne ----------

    public async Task<Schedule> GetScheduleAsync() =>
        await cache.GetOrCreateAsync(
            CacheKeys.Schedule,
            async ct =>
            {
                var hours = await db.OpeningHours.AsNoTracking().Where(h => h.ArchivedAt == null)
                    .OrderBy(h => h.DayOfWeek).ThenBy(h => h.Start).ToListAsync(ct);
                // Les fermetures terminées n'influencent plus rien (marge d'un jour pour la durée du cache).
                var from = RestaurantClock.ToLocal(clock.GetUtcNow()).AddDays(-1);
                var closures = await db.Closures.AsNoTracking().Where(c => c.ArchivedAt == null && c.EndLocal > from)
                    .OrderBy(c => c.StartLocal).ToListAsync(ct);
                return new Schedule(hours, closures);
            },
            CacheKeys.Catalog,
            [CacheKeys.ScheduleTag]
        );

    private async Task SaveWithHistoryAsync(RestaurantSettings settings, CallerIdentity caller)
    {
        var now = clock.GetUtcNow();
        settings.UpdatedAt = now;
        var snapshot = ToDetails(settings);
        db.RestaurantSettingsHistory.Add(new RestaurantSettingsHistory
        {
            ChangedAt = now,
            ChangedByUserId = caller.UserId,
            SnapshotJson = JsonSerializer.Serialize(
                new
                {
                    snapshot.TradeName, snapshot.LegalName, snapshot.Address, snapshot.Phone, snapshot.Siret,
                    snapshot.VatNumber, LogoKey = settings.LogoUrl, snapshot.AsapDelayMinutes, snapshot.SlotDurationMinutes,
                    snapshot.SlotCapacityOrders, snapshot.MinLeadMinutes, snapshot.ApprovalTimeoutMinutes,
                    snapshot.PickupGraceMinutes, snapshot.NoShowThreshold, snapshot.OnlinePaymentExpiryMinutes,
                },
                SnapshotJsonOptions
            ),
        });
        await db.SaveChangesAsync();
    }

    private async Task<RestaurantSettings> ReadSettingsAsync() =>
        await db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == RestaurantSettings.SingletonId)
        ?? new RestaurantSettings();

    private async Task<RestaurantSettings> LoadSettingsAsync() =>
        await db.RestaurantSettings.FirstOrDefaultAsync(s => s.Id == RestaurantSettings.SingletonId)
        ?? throw new InvalidOperationException("Ligne RestaurantSettings absente : migration non appliquée.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OpeningRangeDto ToDto(OpeningHours h) => new() { DayOfWeek = h.DayOfWeek, Start = h.Start, End = h.End };

    private static ClosureDetails ToDto(Closure c) =>
        new() { Id = c.Id, StartLocal = c.StartLocal, EndLocal = c.EndLocal, Reason = c.Reason };

    private RestaurantSettingsDetails ToDetails(RestaurantSettings s) =>
        new()
        {
            TradeName = s.TradeName,
            LegalName = s.LegalName,
            Address = s.Address,
            Phone = s.Phone,
            Siret = s.Siret,
            VatNumber = s.VatNumber,
            LogoUrl = images.Url(s.LogoUrl),
            AsapDelayMinutes = s.AsapDelayMinutes,
            SlotDurationMinutes = s.SlotDurationMinutes,
            SlotCapacityOrders = s.SlotCapacityOrders,
            MinLeadMinutes = s.MinLeadMinutes,
            ApprovalTimeoutMinutes = s.ApprovalTimeoutMinutes,
            PickupGraceMinutes = s.PickupGraceMinutes,
            NoShowThreshold = s.NoShowThreshold,
            OnlinePaymentExpiryMinutes = s.OnlinePaymentExpiryMinutes,
            IsPaused = s.IsPaused,
            PausedUntil = s.PausedUntil,
            UpdatedAt = s.UpdatedAt,
        };
}
