using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using static RestaurantHandler.Controllers.ApiResponses;

namespace RestaurantHandler.Controllers;

/// <summary>Profil professionnel du client connecté : SIREN, raison sociale, TVA, adresse de facturation.</summary>
[ApiController]
[Route("users/me/business-profile")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = nameof(RoleEnum.Client))]
public class BusinessProfileController(BusinessProfileService profiles) : ControllerBase
{
    /// <summary><c>data</c> vaut null si le client n'est pas professionnel.</summary>
    [HttpGet]
    public async Task<ActionResult<Response<BusinessProfileDetails?>>> Get() =>
        Success(await profiles.GetMineAsync(await CallerAsync()));

    /// <summary>Devenir professionnel ou modifier son profil ; n° de TVA calculé depuis le SIREN s'il est omis.</summary>
    [HttpPut]
    [Consumes("application/json")]
    public async Task<ActionResult<Response<BusinessProfileDetails>>> Upsert([FromBody] BusinessProfileUpsert dto) =>
        Success(await profiles.UpsertMineAsync(dto, await CallerAsync()), "Profil professionnel enregistré");

    /// <summary>Redevenir particulier ; les demandes de facture déjà créées sont conservées.</summary>
    [HttpDelete]
    public async Task<ActionResult<Response<object>>> Delete()
    {
        await profiles.DeleteMineAsync(await CallerAsync());
        return Done("Profil professionnel supprimé");
    }

    private async Task<CallerIdentity> CallerAsync() =>
        await this.GetCallerAsync() ?? throw new BusinessForbiddenException("Compte introuvable.");
}

/// <summary>Demandes de facture électronique (US-E05-6).</summary>
[ApiController]
[Produces("application/json")]
[EnableCors]
[Authorize]
public class InvoiceRequestsController(EInvoicingService invoicing) : ControllerBase
{
    /// <summary>Demande de facture d'une commande (propriétaire ou Manager) ; <c>data</c> null si aucune.</summary>
    [HttpGet("orders/{orderId:guid}/invoice-request")]
    public async Task<ActionResult<Response<InvoiceRequestDetails?>>> ForOrder(Guid orderId) =>
        Success(await invoicing.GetForOrderAsync(orderId, await CallerAsync()));

    /// <summary>Toutes les demandes, les plus récentes d'abord, filtrables par statut.</summary>
    [HttpGet("manage/invoice-requests")]
    [Authorize(Roles = nameof(RoleEnum.Manager))]
    public async Task<ActionResult<Response<List<InvoiceRequestDetails>>>> List([FromQuery] InvoiceRequestFilter filter)
    {
        var (items, total) = await invoicing.ListAsync(filter);
        return new OkObjectResult(new Response<List<InvoiceRequestDetails>> { Status = 200, Message = "Ok", Data = items, Count = total });
    }

    /// <summary>Relance immédiate d'une demande en attente ou en échec.</summary>
    [HttpPost("manage/invoice-requests/{id:guid}/retry")]
    [Authorize(Roles = nameof(RoleEnum.Manager))]
    public async Task<ActionResult<Response<InvoiceRequestDetails>>> Retry(Guid id) =>
        Success(await invoicing.RetryAsync(id));

    private async Task<CallerIdentity> CallerAsync() =>
        await this.GetCallerAsync() ?? throw new BusinessForbiddenException("Compte introuvable.");
}
