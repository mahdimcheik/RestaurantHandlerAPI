using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Models.Files;
using RestaurantHandler.Models.Filters;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class TeachersController : ControllerBase
{
    private readonly TeacherSearchService _searchService;
    private readonly TeacherProfileService _teacherProfileService;
    private readonly TeacherDocumentService _documentService;
    private readonly ProfileService _profileService;

    public TeachersController(
        TeacherSearchService searchService,
        TeacherProfileService teacherProfileService,
        TeacherDocumentService documentService,
        ProfileService profileService
    )
    {
        _searchService = searchService;
        _teacherProfileService = teacherProfileService;
        _documentService = documentService;
        _profileService = profileService;
    }
    [AllowAnonymous]
    [HttpPost("all")]
    public async Task<ActionResult<Response<List<UserDetails>>>> GetAllTeacherProfiles([FromBody]FilterTeacher filters)
    {
        var response = await _searchService.GetAllTeacherProfilesAsync(filters, await this.GetCallerAsync());

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("featured")]
    public async Task<ActionResult<Response<List<UserDetails>>>> GetFeaturedTeachers()
    {
        var response = await _searchService.GetFeaturedTeachersAsync();
        return StatusCode(response.Status, response);
    }

    [HttpGet("my-profile")]
    [Authorize]
    public async Task<ActionResult<Response<UserDetails>>> GetMyTeacherProfile()
    {
        var response = await _teacherProfileService.GetTeacherFullProfileAsync(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<Response<UserDetails>>> GetTeacherProfileByUserId(
        [FromRoute] Guid userId)
    {
        var response = await _profileService.GetTeacherProfileByUserIdAsync(userId);

        if (response.Status == 200)
        {
            return Ok(response);
        }

        return StatusCode(response.Status, response);
    }

    [Authorize]
    [HttpGet("user/{userId:guid}/phone")]
    public async Task<ActionResult<Response<string>>> GetTeacherPhone([FromRoute] Guid userId)
    {
        var response = await _profileService.GetTeacherPhoneAsync(userId);
        return StatusCode(response.Status, response);
    }

    [Authorize]
    [HttpGet("user/{userId:guid}/email")]
    public async Task<ActionResult<Response<string>>> GetTeacherEmail([FromRoute] Guid userId)
    {
        var response = await _profileService.GetTeacherEmailAsync(userId);
        return StatusCode(response.Status, response);
    }

    [HttpPut("update-profile")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<UserDetails>>> UpdateTeacherProfile(
        [FromBody] UserUpdate teacherUpdateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _teacherProfileService.UpdateTeacherProfileAsync(teacherUpdateDto, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [HttpPut("switch-to-professional")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<UserDetails>>> SwitchToProfessional(
        [FromBody] TeacherSwitchToProfessional dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _teacherProfileService.SwitchToProfessionalAsync(dto, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [HttpGet("get-documents")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<List<PrivacyDocumentDetails>>>> GetPrivacyDocuments()
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _documentService.GetDocumentsByTeacher(await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [HttpPost("upload-document")]
    [Authorize(Roles = "Teacher")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<PrivacyDocumentDetails>>> UploadDocument(
        [FromForm] UploadPrivacyDocument dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new Response<object>
            {
                Status = 400,
                Message = "Données de validation invalides",
                Data = ModelState
            });
        }

        var response = await _documentService.UploadPrivacyDocumentAsync(dto, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [HttpDelete("delete-document/{documentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<object>>> DeleteDocument(
        [FromRoute] Guid documentId)
    {
        var response = await _documentService.DeletePrivacyDocumentAsync(documentId, await this.GetCallerAsync());

        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("document-types")]
    public async Task<ActionResult<Response<List<PrivacyDocumentTypeDetails>>>> GetPrivacyTypes(
    )
    {
        var response = await _documentService.PrivacyDocumentTypesAsync();
        return StatusCode(response.Status, response);
    }

    [HttpGet("document-requests")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<List<TeacherDocumentRequestDetails>>>> GetDocumentRequests()
    {
        var response = await _documentService.GetDocumentRequestsByTeacher(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }
}
