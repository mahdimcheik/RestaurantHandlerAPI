using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Contrôleur pour la gestion des utilisateurs côté administration.
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
[Authorize(Roles = "Admin,SuperAdmin")]
public class UserManagementController : ControllerBase
{
    private readonly UserManagementService _userManagementService;

    public UserManagementController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    /// <summary>
    /// Liste paginée des enseignants avec filtres (rôle, confirmation, nom).
    /// </summary>
    [HttpPost("teachers")]
    public async Task<ActionResult<Response<List<UserDetails>>>> GetTeachers(
        [FromBody] FilterUserManagement filters
    )
    {
        var response = await _userManagementService.GetTeachersAsync(filters);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Détail de la vérification SIRET d'un enseignant (dénomination et adresse INSEE).
    /// Réservé à l'administration : ces données ne sont pas exposées dans TeacherDetails.
    /// </summary>
    [HttpGet("teachers/{userId:guid}/siret-verification")]
    public async Task<ActionResult<Response<TeacherAdminDetails>>> GetTeacherSiretVerification(
        [FromRoute] Guid userId
    )
    {
        var response = await _userManagementService.GetTeacherSiretVerificationAsync(userId);
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Modifie manuellement le statut du compte d'un utilisateur (actif, en attente, banni).
    /// </summary>
    [HttpPatch("users/{userId:guid}/status")]
    public async Task<ActionResult<Response<UserDetails>>> SetUserAccountStatus(
        [FromRoute] Guid userId,
        [FromQuery] Guid statusId
    )
    {
        var response = await _userManagementService.SetUserAccountStatusAsync(userId, statusId);
        return StatusCode(response.Status, response);
    }

    // ----------------------------------------------------------------------
    // Vérification documentaire (Feature 10)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Dossier de vérification documentaire d'un prof : documents vivants (avec revue),
    /// demandes ad hoc ouvertes, état de vérification.
    /// </summary>
    [HttpGet("teachers/{userId:guid}/documents")]
    public async Task<ActionResult<Response<TeacherCertificationOverview>>> GetTeacherDocuments(
        [FromRoute] Guid userId
    )
    {
        var caller = await this.GetCallerAsync();
        if (caller is null)
        {
            return Unauthorized();
        }

        var response = await _userManagementService.GetTeacherDocumentsAsync(userId, caller.UserId);
        return StatusCode(response.Status, response);
    }

    /// <summary>Valide ou refuse un document envoyé par un prof.</summary>
    [HttpPatch("documents/{documentId:guid}/review")]
    public async Task<ActionResult<Response<PrivacyDocumentDetails>>> ReviewDocument(
        [FromRoute] Guid documentId,
        [FromBody] DocumentReviewDecision dto
    )
    {
        var response = await _userManagementService.ReviewDocumentAsync(documentId, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>Demande un document supplémentaire à un prof spécifique.</summary>
    [HttpPost("teachers/{userId:guid}/document-requests")]
    public async Task<ActionResult<Response<TeacherDocumentRequestDetails>>> RequestDocument(
        [FromRoute] Guid userId,
        [FromBody] DocumentRequestCreate dto
    )
    {
        var response = await _userManagementService.RequestDocumentAsync(userId, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>Annule une demande de document jamais comblée.</summary>
    [HttpDelete("document-requests/{requestId:guid}")]
    public async Task<ActionResult<Response<object>>> CancelDocumentRequest(
        [FromRoute] Guid requestId
    )
    {
        var response = await _userManagementService.CancelDocumentRequestAsync(requestId);
        return StatusCode(response.Status, response);
    }

    // ----------------------------------------------------------------------
    // Catalogue des types de document
    // ----------------------------------------------------------------------

    [HttpPost("document-types")]
    public async Task<ActionResult<Response<PrivacyDocumentTypeDetails>>> CreateDocumentType(
        [FromBody] PrivacyDocumentTypeCreate dto
    )
    {
        var response = await _userManagementService.CreateDocumentTypeAsync(dto);
        return StatusCode(response.Status, response);
    }

    [HttpPut("document-types/{id:guid}")]
    public async Task<ActionResult<Response<PrivacyDocumentTypeDetails>>> UpdateDocumentType(
        [FromRoute] Guid id,
        [FromBody] PrivacyDocumentTypeUpdate dto
    )
    {
        var response = await _userManagementService.UpdateDocumentTypeAsync(id, dto);
        return StatusCode(response.Status, response);
    }

    [HttpDelete("document-types/{id:guid}")]
    public async Task<ActionResult<Response<object>>> DeleteDocumentType([FromRoute] Guid id)
    {
        var response = await _userManagementService.DeleteDocumentTypeAsync(id);
        return StatusCode(response.Status, response);
    }
}
