using Microsoft.EntityFrameworkCore;
using RestaurantHandler.Contexts;
using RestaurantHandler.Domain.Policies;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Services;

/// <summary>
/// Validation d'une commande en ligne payée par carte (US-E04-4, ADR-0013). Tout est revérifié côté
/// serveur ; la création se fait sous un verrou applicatif unique pour que capacité des créneaux,
/// numéro du jour et idempotence restent justes en cas de validations simultanées (R7, R8, R10).
/// </summary>
public class OrderPlacementService(
    MainContext db,
    CartService carts,
    RestaurantService restaurant,
    OrderNotifier notifier,
    TimeProvider clock
)
{
    /// <summary>Clé du verrou <c>pg_advisory_xact_lock</c> qui sérialise les créations de commandes.</summary>
    private const long PlacementLockKey = 0x5E57_0001;

    public async Task<OrderDetails> PlaceOnlineAsync(OrderCreate dto, CallerIdentity caller)
    {
        if (await FindExistingAsync(caller.UserId, dto.IdempotencyKey) is { } replay)
            return replay;

        var cart = await carts.PriceAsync(dto.Lines);
        if (!cart.IsValid)
            throw new CartRejectedException(cart.Issues);

        if (!EnvironmentVariables.SumUpEnabled)
            throw new BusinessConflictException(
                "Le paiement en ligne est momentanément indisponible : la commande ne peut pas être validée."
            );

        var settings = await ReadSettingsAsync();

        var (order, created) = await CreateUnderLockAsync(
            caller.UserId,
            dto.IdempotencyKey,
            async now =>
            {
                var schedule = await restaurant.GetScheduleAsync();
                var opening = OpeningPolicy.Compute(
                    now,
                    schedule.Hours,
                    schedule.Closures,
                    settings.IsPaused,
                    settings.PausedUntil
                );
                if (OpeningPolicy.IsPauseActive(settings.IsPaused, settings.PausedUntil, now))
                    throw new BusinessConflictException(
                        "Les commandes en ligne sont momentanément en pause. Réessayez dans quelques minutes."
                    );

                DateTimeOffset pickupAt;
                if (dto.PickupMode == PickupMode.Asap)
                {
                    if (opening.Status != OpeningStatus.Open)
                        throw new BusinessConflictException(
                            "Greenadine est fermé : choisissez un créneau."
                        );
                    pickupAt = now.AddMinutes(settings.AsapDelayMinutes);
                }
                else
                {
                    var slot =
                        (
                            await restaurant.SlotsWithOccupancyAsync(now, schedule, settings)
                        ).FirstOrDefault(s => s.Start == dto.SlotStart)
                        ?? throw new BusinessConflictException(
                            "Ce créneau n'est plus disponible : choisissez-en un autre."
                        );
                    pickupAt = slot.Start;
                }

                return new Order
                {
                    CustomerId = caller.UserId,
                    Status = OrderStatus.PendingPayment,
                    PickupMode = dto.PickupMode,
                    PickupAt = pickupAt,
                    CustomerNote = Clean(dto.CustomerNote),
                };
            },
            cart
        );

        if (created)
            await notifier.CreatedAsync(order);
        return await LoadDetailsAsync(order.Id);
    }

    /// <summary>
    /// Sous verrou : rejeu idempotent, contrôles d'ouverture et de créneau (via
    /// <paramref name="prepare"/>), numéro du jour, lignes, historique.
    /// </summary>
    private async Task<(Order Order, bool Created)> CreateUnderLockAsync(
        Guid authorId,
        string idempotencyKey,
        Func<DateTimeOffset, Task<Order>> prepare,
        PricedCart cart
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", PlacementLockKey);

        var existing = await db.Orders.FirstOrDefaultAsync(o =>
            o.CreatedByUserId == authorId && o.IdempotencyKey == idempotencyKey
        );
        if (existing is not null)
            return (existing, false);

        var now = clock.GetUtcNow();
        var order = await prepare(now);
        order.CreatedByUserId = authorId;
        order.IdempotencyKey = idempotencyKey;
        order.CreatedAt = now;
        order.ServiceDate = DateOnly.FromDateTime(RestaurantClock.ToLocal(order.PickupAt));
        order.DailyNumber =
            (
                await db
                    .Orders.Where(o => o.ServiceDate == order.ServiceDate)
                    .MaxAsync(o => (int?)o.DailyNumber)
                ?? 0
            ) + 1;
        order.TotalCents = cart.TotalCents;

        // Les composants de menu sont rattachés à la commande en plus de leur ligne parente.
        var position = 0;
        foreach (var item in cart.Items)
        {
            item.CreatedAt = now.AddTicks(position++);
            order.Items.Add(item);
            foreach (var child in item.Children)
            {
                child.CreatedAt = item.CreatedAt;
                child.Order = order;
            }
        }

        order.StatusHistory.Add(
            new OrderStatusHistory
            {
                Status = order.Status,
                ChangedByUserId = authorId,
                ChangedAt = now,
            }
        );
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (order, true);
    }

    /// <summary>Disponibilité du paiement en ligne, pour l'écran de validation.</summary>
    public static CheckoutOptions CheckoutOptions() =>
        new() { OnlinePaymentEnabled = EnvironmentVariables.SumUpEnabled };

    private async Task<OrderDetails?> FindExistingAsync(Guid authorId, string idempotencyKey)
    {
        var id = await db
            .Orders.Where(o => o.CreatedByUserId == authorId && o.IdempotencyKey == idempotencyKey)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync();
        return id is null ? null : await LoadDetailsAsync(id.Value);
    }

    private async Task<OrderDetails> LoadDetailsAsync(Guid id)
    {
        var order = await OrderQueryService
            .WithDetails(db.Orders.AsNoTrackingWithIdentityResolution())
            .FirstAsync(o => o.Id == id);
        return OrderMapping.ToDetails(order, false);
    }

    private async Task<RestaurantSettings> ReadSettingsAsync() =>
        await db
            .RestaurantSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == RestaurantSettings.SingletonId)
        ?? new RestaurantSettings();

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
