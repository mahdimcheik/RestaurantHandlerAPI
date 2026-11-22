using RestaurantHandler.Models;

namespace RestaurantHandler.Domain.Policies;

public enum OrderActor
{
    Customer,
    Manager,

    /// <summary>Tâches planifiées et notifications de paiement.</summary>
    System,
}

/// <summary>Ce que la politique doit savoir de la commande pour décider.</summary>
public sealed record TransitionContext(
    DateTimeOffset Now,
    bool IsPaid,
    DateTimeOffset PickupAt,
    int PickupGraceMinutes,
    DateTimeOffset StatusSince,
    string? Reason
);

/// <summary>
/// Machine à états (docs/20-regles-metier/02-machine-etats-commande.md). Toute transition non
/// listée est refusée ; les états finaux sont immuables.
/// </summary>
public static class OrderTransitionPolicy
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(10);

    public static readonly OrderStatus[] FinalStatuses =
    [
        OrderStatus.Completed,
        OrderStatus.NoShow,
        OrderStatus.Refused,
        OrderStatus.Cancelled,
    ];

    /// <summary>Statuts qui occupent un créneau et la cuisine.</summary>
    public static readonly OrderStatus[] ActiveStatuses =
    [
        OrderStatus.PendingPayment,
        OrderStatus.AwaitingApproval,
        OrderStatus.Confirmed,
        OrderStatus.InPreparation,
        OrderStatus.Ready,
    ];

    public static bool IsFinal(OrderStatus status) => FinalStatuses.Contains(status);

    /// <summary>Message de refus destiné à l'utilisateur, ou null si la transition est permise.</summary>
    public static string? Check(OrderStatus from, OrderStatus to, OrderActor actor, TransitionContext ctx)
    {
        if (IsFinal(from))
            return "Cette commande est terminée : son statut ne peut plus changer.";

        var manager = actor == OrderActor.Manager;
        var system = actor == OrderActor.System;
        var customer = actor == OrderActor.Customer;
        var hasReason = !string.IsNullOrWhiteSpace(ctx.Reason);

        string? Denied() => $"Transition {from} → {to} non autorisée.";
        string? RequireReason() => hasReason ? null : "Un motif est obligatoire.";

        return (from, to) switch
        {
            (OrderStatus.PendingPayment, OrderStatus.Confirmed) => system ? null : Denied(),
            // Paiement en ligne échoué : le client bascule au retrait (validation Manager ensuite).
            (OrderStatus.PendingPayment, OrderStatus.AwaitingApproval) => customer ? null : Denied(),
            (OrderStatus.PendingPayment, OrderStatus.Cancelled) =>
                customer || system ? null : manager ? RequireReason() : Denied(),

            (OrderStatus.AwaitingApproval, OrderStatus.Confirmed) => manager ? null : Denied(),
            (OrderStatus.AwaitingApproval, OrderStatus.Refused) =>
                system ? null : manager ? RequireReason() : Denied(),
            (OrderStatus.AwaitingApproval, OrderStatus.Cancelled) =>
                customer ? null : manager ? RequireReason() : Denied(),

            (OrderStatus.Confirmed, OrderStatus.InPreparation) => manager ? null : Denied(),
            (OrderStatus.Confirmed, OrderStatus.Cancelled) =>
                customer ? null : manager ? RequireReason() : Denied(),

            (OrderStatus.InPreparation, OrderStatus.Ready) => manager ? null : Denied(),
            (OrderStatus.InPreparation, OrderStatus.Confirmed) => manager ? Undo(ctx) : Denied(),
            (OrderStatus.InPreparation, OrderStatus.Cancelled) => manager ? RequireReason() : Denied(),

            (OrderStatus.Ready, OrderStatus.Completed) =>
                !manager ? Denied()
                : ctx.IsPaid ? null
                : "La commande n'est pas payée : déclarez l'encaissement avant de la remettre.",
            (OrderStatus.Ready, OrderStatus.NoShow) =>
                !manager ? Denied()
                : ctx.Now >= ctx.PickupAt.AddMinutes(ctx.PickupGraceMinutes) ? null
                : $"Trop tôt : « non venu » possible à partir de {ctx.PickupAt.AddMinutes(ctx.PickupGraceMinutes):HH:mm} UTC.",
            (OrderStatus.Ready, OrderStatus.InPreparation) => manager ? Undo(ctx) : Denied(),
            (OrderStatus.Ready, OrderStatus.Cancelled) => manager ? RequireReason() : Denied(),

            _ => Denied(),
        };
    }

    private static string? Undo(TransitionContext ctx) =>
        ctx.Now - ctx.StatusSince <= UndoWindow ? null : "Le délai d'annulation du geste (10 s) est dépassé.";
}
