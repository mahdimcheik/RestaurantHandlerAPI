using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>Articles scientifiques : lecture publique des articles publiés, rédaction par les admins et les profs éditeurs.</summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class ArticlesController(
    ArticleService articleService,
    ArticleImageService imageService,
    ArticleCommentService commentService,
    ArticleStatsService statsService
) : ControllerBase
{
    // ── Public ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("published")]
    public async Task<ActionResult<Response<List<ArticleSummary>>>> GetPublished([FromBody] ArticleQuery query)
    {
        var response = await articleService.GetPublishedAsync(query);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<Response<ArticleDetails>>> GetBySlug([FromRoute] string slug)
    {
        var response = await articleService.GetPublishedBySlugAsync(slug);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("tags")]
    public async Task<ActionResult<Response<List<string>>>> GetTags([FromQuery] string? language)
    {
        var response = await articleService.GetTagsAsync(language);
        return StatusCode(response.Status, response);
    }

    /// <summary>Articles publiés de même langue partageant au moins un tag avec l'article.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/related")]
    public async Task<ActionResult<Response<List<ArticleSummary>>>> GetRelated([FromRoute] Guid id, [FromQuery] int count = 5)
    {
        var response = await articleService.GetRelatedAsync(id, count);
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("sitemap")]
    public async Task<ActionResult<Response<List<ArticleSitemapEntry>>>> GetSitemap()
    {
        var response = await articleService.GetSitemapAsync();
        return StatusCode(response.Status, response);
    }

    /// <summary>Compte un affichage de l'article publié, appelé par le navigateur au chargement de la page.</summary>
    [AllowAnonymous]
    [HttpPost("{id:guid}/view")]
    public async Task<ActionResult<Response<object>>> RecordView([FromRoute] Guid id)
    {
        var response = await statsService.RecordViewAsync(id);
        return StatusCode(response.Status, response);
    }

    // ── Administration ──────────────────────────────────────────────────────────────────

    [HttpPost("admin/all")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<List<ArticleSummary>>>> GetAdminList([FromBody] ArticleQuery query)
    {
        var response = await articleService.GetAdminListAsync(query);
        return StatusCode(response.Status, response);
    }

    /// <summary>Articles rédigés par l'appelant, quel que soit leur statut (espace éditeur).</summary>
    [HttpPost("mine")]
    [Authorize(Roles = "Editor")]
    public async Task<ActionResult<Response<List<ArticleSummary>>>> GetMine([FromBody] ArticleQuery query)
    {
        var caller = await this.GetCallerAsync();
        if (caller is null)
            return Unauthorized();

        var response = await articleService.GetAdminListAsync(query, caller.UserId);
        return StatusCode(response.Status, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<ArticleDetails>>> GetById([FromRoute] Guid id)
    {
        var response = await articleService.GetByIdAsync(id, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<ArticleDetails>>> Create([FromBody] ArticleUpsert dto)
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await articleService.CreateAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<ArticleDetails>>> Update([FromRoute] Guid id, [FromBody] ArticleUpsert dto)
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await articleService.UpdateAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/publish")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<ArticleDetails>>> Publish([FromRoute] Guid id)
    {
        var response = await articleService.SetPublishedAsync(id, true, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/unpublish")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<ArticleDetails>>> Unpublish([FromRoute] Guid id)
    {
        var response = await articleService.SetPublishedAsync(id, false, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    public async Task<ActionResult<Response<object>>> Archive([FromRoute] Guid id)
    {
        var response = await articleService.ArchiveAsync(id, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("images")]
    [Authorize(Roles = "Admin,SuperAdmin,Editor")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ArticleImageService.MAX_SIZE_BYTES + 1024 * 1024)]
    public async Task<ActionResult<Response<ArticleImage>>> UploadImage([FromForm] ArticleImageUpload dto, CancellationToken ct)
    {
        var response = await imageService.UploadAsync(dto.File, dto.Cover, ct);
        return StatusCode(response.Status, response);
    }

    // ── Statistiques (SuperAdmin) ───────────────────────────────────────────────────────

    [HttpGet("stats")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<Response<ArticleStatsOverview>>> GetStats([FromQuery] int? days)
    {
        var response = await statsService.GetOverviewAsync(days);
        return StatusCode(response.Status, response);
    }

    [HttpGet("{id:guid}/stats")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<Response<ArticleStatsDetails>>> GetArticleStats([FromRoute] Guid id, [FromQuery] int? days)
    {
        var response = await statsService.GetArticleAsync(id, days);
        return StatusCode(response.Status, response);
    }

    // ── Commentaires ────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost("{id:guid}/comments/list")]
    public async Task<ActionResult<Response<List<ArticleCommentDetails>>>> GetComments(
        [FromRoute] Guid id,
        [FromBody] CustomTableState query
    )
    {
        var response = await commentService.ListAsync(id, query);
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    public async Task<ActionResult<Response<ArticleCommentDetails>>> AddComment(
        [FromRoute] Guid id,
        [FromBody] ArticleCommentCreate dto
    )
    {
        if (!ModelState.IsValid)
            return Invalid();

        var response = await commentService.CreateAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpDelete("comments/{commentId:guid}")]
    [Authorize]
    public async Task<ActionResult<Response<object>>> DeleteComment([FromRoute] Guid commentId)
    {
        var response = await commentService.DeleteAsync(commentId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("comments/{commentId:guid}/hide")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<Response<object>>> HideComment([FromRoute] Guid commentId)
    {
        var response = await commentService.HideAsync(commentId);
        return StatusCode(response.Status, response);
    }

    private BadRequestObjectResult Invalid() =>
        BadRequest(new Response<object> { Status = 400, Message = "Données de validation invalides", Data = ModelState });
}
