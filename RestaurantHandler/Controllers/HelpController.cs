using RestaurantHandler.Models;
using RestaurantHandler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>Centre d'aide : lecture publique des fiches publiées, rédaction par les admins.</summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class HelpController(HelpService helpService) : ControllerBase
{
    // ── Public ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet("published")]
    public async Task<ActionResult<Response<List<HelpSummary>>>> GetPublished([FromQuery] string? language)
    {
        var response = await helpService.GetPublishedAsync(language);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpPost("search")]
    public async Task<ActionResult<Response<List<HelpSummary>>>> Search([FromBody] HelpSearchQuery query)
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await helpService.SearchAsync(query);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<Response<HelpDetails>>> GetBySlug([FromRoute] string slug)
    {
        var response = await helpService.GetPublishedBySlugAsync(slug);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("sitemap")]
    public async Task<ActionResult<Response<List<HelpSitemapEntry>>>> GetSitemap()
    {
        var response = await helpService.GetSitemapAsync();
        return StatusCode(response.Status, response);
    }

    // ── Administration ──────────────────────────────────────────────────────────────────

    [HttpPost("admin/all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<List<HelpSummary>>>> GetAdminList([FromBody] HelpAdminQuery query)
    {
        var response = await helpService.GetAdminListAsync(query);
        return StatusCode(response.Status, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<HelpDetails>>> GetById([FromRoute] Guid id)
    {
        var response = await helpService.GetByIdAsync(id);
        return StatusCode(response.Status, response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<HelpDetails>>> Create([FromBody] HelpUpsert dto)
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await helpService.CreateAsync(dto);
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<HelpDetails>>> Update([FromRoute] Guid id, [FromBody] HelpUpsert dto)
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await helpService.UpdateAsync(id, dto);
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/publish")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<HelpDetails>>> Publish([FromRoute] Guid id)
    {
        var response = await helpService.SetPublishedAsync(id, true);
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/unpublish")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<HelpDetails>>> Unpublish([FromRoute] Guid id)
    {
        var response = await helpService.SetPublishedAsync(id, false);
        return StatusCode(response.Status, response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<object>>> Archive([FromRoute] Guid id)
    {
        var response = await helpService.ArchiveAsync(id);
        return StatusCode(response.Status, response);
    }

    private BadRequestObjectResult Invalid() =>
        BadRequest(new Response<object> { Status = 400, Message = "Données de validation invalides", Data = ModelState });
}
