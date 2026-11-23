using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Infrastructure.RateLimiting;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using static RestaurantHandler.Controllers.ApiResponses;

namespace RestaurantHandler.Controllers;

/// <summary>Commandes côté client : devis du panier, validation, suivi, annulation.</summary>
[ApiController]
[Route("orders")]
[Produces("application/json")]
[EnableCors]
[Authorize]
public class OrdersController(
    CartService carts,
    OrderPlacementService placement,
    OrderQueryService queries,
    OnlinePaymentService payments
) : ControllerBase
{
    /// <summary>Prix et contrôles du panier sans rien créer ; 409 avec <c>issues</c> si le panier doit changer.</summary>
    [AllowAnonymous]
    [HttpPost("quote")]
    public async Task<ActionResult<Response<QuoteResult>>> Quote([FromBody] QuoteRequest dto) =>
        Success(await carts.QuoteAsync(dto));

    /// <summary>
    /// Valide le panier (US-E04-4). Un rejeu avec la même clé renvoie la commande déjà créée. En
    /// paiement en ligne, <c>paymentUrl</c> mène à la page SumUp ; si elle n'a pas pu être créée,
    /// la commande existe quand même et <c>POST /orders/{id}/payment</c> relance la tentative.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(RoleEnum.Client))]
    [EnableRateLimiting(RateLimitingSetup.OrderPolicy)]
    public async Task<ActionResult<Response<OrderDetails>>> Place([FromBody] OrderCreate dto)
    {
        var caller = await CallerAsync();
        var order = await placement.PlaceOnlineAsync(dto, caller);
        if (order.Status != OrderStatus.PendingPayment || order.PaymentUrl is not null)
            return CreatedWith(order, "Commande transmise");

        try
        {
            return CreatedWith(await payments.StartCheckoutAsync(order.Id, caller), "Commande transmise : paiement en attente");
        }
        catch (BusinessException ex)
        {
            return CreatedWith(order, ex.Message);
        }
    }

    /// <summary>Nouvelle tentative de paiement en ligne (après échec ou abandon).</summary>
    [HttpPost("{id:guid}/payment")]
    [Authorize(Roles = nameof(RoleEnum.Client))]
    [EnableRateLimiting(RateLimitingSetup.OrderPolicy)]
    public async Task<ActionResult<Response<OrderDetails>>> RetryPayment(Guid id) =>
        Success(await payments.StartCheckoutAsync(id, await CallerAsync()));

    /// <summary>Abandonne le paiement en ligne pour le paiement au retrait (validation Manager).</summary>
    [HttpPost("{id:guid}/pay-on-site")]
    [Authorize(Roles = nameof(RoleEnum.Client))]
    public async Task<ActionResult<Response<OrderDetails>>> PayOnSite(Guid id) =>
        Success(await payments.SwitchToPayOnSiteAsync(id, await CallerAsync()));

    /// <summary>Justificatif d'une commande payée (US-E05-5).</summary>
    [HttpGet("{id:guid}/receipt")]
    public async Task<ActionResult<Response<OrderReceipt>>> Receipt(Guid id) =>
        Success(await queries.ReceiptAsync(id, await CallerAsync()));

    [HttpGet("mine")]
    public async Task<ActionResult<Response<List<OrderSummary>>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await queries.ListMineAsync(await CallerAsync(), page, pageSize);
        return new OkObjectResult(new Response<List<OrderSummary>> { Status = 200, Message = "Ok", Data = items, Count = total });
    }

    /// <summary>Commandes en cours du client (bandeau).</summary>
    [HttpGet("mine/current")]
    public async Task<ActionResult<Response<List<OrderSummary>>>> Current() =>
        Success(await queries.CurrentAsync(await CallerAsync()));

    /// <summary>Le propriétaire ou un Manager ; 404 pour tout autre.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<OrderDetails>>> Get(Guid id) =>
        Success(await queries.GetAsync(id, await CallerAsync()));

    /// <summary>Avant préparation ; un paiement en ligne est remboursé automatiquement.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Response<OrderDetails>>> Cancel(Guid id) =>
        Success(await payments.CancelByCustomerAsync(id, await CallerAsync()), "Commande annulée");

    private async Task<CallerIdentity> CallerAsync() =>
        await this.GetCallerAsync() ?? throw new BusinessForbiddenException("Compte introuvable.");
}

/// <summary>Commandes côté restaurant : comptoir, liste du jour, KDS, transitions, encaissements.</summary>
[ApiController]
[Route("manage/orders")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = nameof(RoleEnum.Manager))]
public class ManageOrdersController(
    OrderPlacementService placement,
    OrderQueryService queries,
    OrderWorkflowService workflow,
    OnlinePaymentService payments
) : ControllerBase
{
    /// <summary>Saisie comptoir (US-E06-1) : `Confirmée` d'emblée, non bloquée par la pause.</summary>
    [HttpPost("counter")]
    public async Task<ActionResult<Response<OrderDetails>>> Counter([FromBody] CounterOrderCreate dto) =>
        CreatedWith(await placement.PlaceCounterAsync(dto, await CallerAsync()), "Commande envoyée en cuisine");

    /// <summary>Commandes d'un jour de service (aujourd'hui par défaut), filtrables.</summary>
    [HttpGet]
    public async Task<ActionResult<Response<List<OrderSummary>>>> List([FromQuery] OrderFilter filter) =>
        Success(await queries.ListForManagerAsync(filter));

    /// <summary>File du KDS.</summary>
    [HttpGet("kitchen")]
    public async Task<ActionResult<Response<List<OrderDetails>>>> Kitchen() => Success(await queries.KitchenAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<OrderDetails>>> Get(Guid id) =>
        Success(await queries.GetAsync(id, await CallerAsync()));

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<Response<OrderDetails>>> Accept(Guid id) =>
        Success(await workflow.AcceptAsync(id, await CallerAsync()));

    [HttpPost("{id:guid}/refuse")]
    public async Task<ActionResult<Response<OrderDetails>>> Refuse(Guid id, [FromBody] ReasonRequest dto) =>
        Success(await workflow.RefuseAsync(id, dto.Reason, await CallerAsync()));

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<Response<OrderDetails>>> Start(Guid id) =>
        Success(await workflow.StartAsync(id, await CallerAsync()));

    [HttpPost("{id:guid}/ready")]
    public async Task<ActionResult<Response<OrderDetails>>> Ready(Guid id) =>
        Success(await workflow.ReadyAsync(id, await CallerAsync()));

    /// <summary>Annule le dernier geste cuisine (10 s).</summary>
    [HttpPost("{id:guid}/undo")]
    public async Task<ActionResult<Response<OrderDetails>>> Undo(Guid id) =>
        Success(await workflow.UndoAsync(id, await CallerAsync()));

    /// <summary>Remise au client : la commande doit être payée.</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<Response<OrderDetails>>> Complete(Guid id) =>
        Success(await workflow.CompleteAsync(id, await CallerAsync()));

    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<Response<OrderDetails>>> NoShow(Guid id) =>
        Success(await workflow.NoShowAsync(id, await CallerAsync()));

    /// <summary>Annulation avec motif ; un paiement en ligne est remboursé automatiquement.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Response<OrderDetails>>> Cancel(Guid id, [FromBody] ReasonRequest dto) =>
        Success(await payments.CancelByManagerAsync(id, dto.Reason, await CallerAsync()));

    /// <summary>Remboursement total des paiements en ligne d'une commande annulée, refusée ou non retirée.</summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<Response<OrderDetails>>> Refund(Guid id) => Success(await payments.RefundAsync(id));

    /// <summary>Déclare un encaissement sur place (carte, espèces, autre) ; montant par défaut = reste à payer.</summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<Response<OrderDetails>>> DeclarePayment(Guid id, [FromBody] PaymentDeclare dto) =>
        Success(await workflow.DeclarePaymentAsync(id, dto, await CallerAsync()));

    /// <summary>Commandes d'un client, les plus récentes d'abord (fiche client, US-E10-1).</summary>
    [HttpGet("/manage/customers/{userId:guid}/orders")]
    public async Task<ActionResult<Response<List<OrderSummary>>>> CustomerOrders(
        Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await queries.ListForCustomerAsync(userId, page, pageSize);
        return new OkObjectResult(new Response<List<OrderSummary>> { Status = 200, Message = "Ok", Data = items, Count = total });
    }

    /// <summary>Lève le blocage du paiement au retrait d'un client.</summary>
    [HttpPost("/manage/customers/{userId:guid}/no-shows/reset")]
    public async Task<ActionResult<Response<object>>> ResetNoShows(Guid userId)
    {
        await workflow.ResetNoShowsAsync(userId, await CallerAsync());
        return Done("Compteur de non-venues remis à zéro");
    }

    private async Task<CallerIdentity> CallerAsync() =>
        await this.GetCallerAsync() ?? throw new BusinessForbiddenException("Compte introuvable.");
}
