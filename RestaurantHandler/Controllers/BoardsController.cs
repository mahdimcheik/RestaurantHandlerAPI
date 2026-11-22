using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

/// <summary>
/// Tableaux de suivi (E20). Routes cibles du contrat d'API, nées sous leur forme finale : pas
/// de verbe dans l'adresse hors actions métier, tout adressé sous son tableau. Rôles posés par
/// action : un attribut de classe s'ajouterait à ceux des actions de lecture.
/// </summary>
[Produces("application/json")]
[Consumes("application/json")]
[Route("boards")]
[ApiController]
[EnableCors]
public class BoardsController(BoardService boardService) : ControllerBase
{
    [HttpPost("search")]
    [Authorize(Roles = "Teacher,Student")]
    public async Task<ActionResult<Response<List<BoardSummary>>>> SearchBoards(
        [FromBody] CustomTableState query
    )
    {
        var response = await boardService.SearchBoardsAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardDetails>>> CreateBoard([FromBody] BoardCreate dto)
    {
        var response = await boardService.CreateBoardAsync(dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Teacher,Student")]
    public async Task<ActionResult<Response<BoardDetails>>> GetBoard([FromRoute] Guid id)
    {
        var response = await boardService.GetBoardAsync(id, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardSummary>>> RenameBoard(
        [FromRoute] Guid id,
        [FromBody] BoardUpdate dto
    )
    {
        var response = await boardService.RenameBoardAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/archive")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardSummary>>> ArchiveBoard([FromRoute] Guid id)
    {
        var response = await boardService.SetBoardArchivedAsync(id, true, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/unarchive")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardSummary>>> UnarchiveBoard([FromRoute] Guid id)
    {
        var response = await boardService.SetBoardArchivedAsync(id, false, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/lists")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardListDetails>>> AddList(
        [FromRoute] Guid id,
        [FromBody] BoardListCreate dto
    )
    {
        var response = await boardService.AddListAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}/lists/order")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<List<BoardListDetails>>>> ReorderLists(
        [FromRoute] Guid id,
        [FromBody] BoardListOrder dto
    )
    {
        var response = await boardService.ReorderListsAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}/lists/{listId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardListDetails>>> RenameList(
        [FromRoute] Guid id,
        [FromRoute] Guid listId,
        [FromBody] BoardListUpdate dto
    )
    {
        var response = await boardService.RenameListAsync(id, listId, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpDelete("{id:guid}/lists/{listId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<object>>> DeleteList(
        [FromRoute] Guid id,
        [FromRoute] Guid listId
    )
    {
        var response = await boardService.DeleteListAsync(id, listId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/cards/search")]
    [Authorize(Roles = "Teacher,Student")]
    public async Task<ActionResult<Response<List<BoardCardTile>>>> SearchCards(
        [FromRoute] Guid id,
        [FromBody] CustomTableState query
    )
    {
        var response = await boardService.SearchCardsAsync(id, query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpGet("{id:guid}/cards/{cardId:guid}")]
    [Authorize(Roles = "Teacher,Student")]
    public async Task<ActionResult<Response<BoardCardDetails>>> GetCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId
    )
    {
        var response = await boardService.GetCardAsync(id, cardId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/cards")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> CreateCard(
        [FromRoute] Guid id,
        [FromBody] BoardCardCreate dto
    )
    {
        var response = await boardService.CreateCardAsync(id, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPut("{id:guid}/cards/{cardId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> UpdateCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId,
        [FromBody] BoardCardUpdate dto
    )
    {
        var response = await boardService.UpdateCardAsync(id, cardId, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/cards/{cardId:guid}/move")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> MoveCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId,
        [FromBody] BoardCardMove dto
    )
    {
        var response = await boardService.MoveCardAsync(id, cardId, dto, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/cards/{cardId:guid}/archive")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> ArchiveCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId
    )
    {
        var response = await boardService.SetCardArchivedAsync(id, cardId, true, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/cards/{cardId:guid}/unarchive")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> UnarchiveCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId
    )
    {
        var response = await boardService.SetCardArchivedAsync(id, cardId, false, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/cards/{cardId:guid}/share")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> ShareCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId
    )
    {
        var response = await boardService.SetCardSharedAsync(id, cardId, true, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPatch("{id:guid}/cards/{cardId:guid}/unshare")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<BoardCardDetails>>> UnshareCard(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId
    )
    {
        var response = await boardService.SetCardSharedAsync(id, cardId, false, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [HttpPost("{id:guid}/cards/{cardId:guid}/attachments")]
    [Authorize(Roles = "Teacher")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(HardCode.ATTACHMENT_MAX_SIZE_BYTES + 1024 * 1024)]
    public async Task<ActionResult<Response<BoardCardAttachmentDetails>>> AddAttachment(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId,
        [FromForm] BoardCardAttachmentUpload dto,
        CancellationToken ct
    )
    {
        var response = await boardService.AddAttachmentAsync(id, cardId, dto.File, await this.GetCallerAsync(), ct);
        return StatusCode(response.Status, response);
    }

    [HttpDelete("{id:guid}/cards/{cardId:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<Response<object>>> DeleteAttachment(
        [FromRoute] Guid id,
        [FromRoute] Guid cardId,
        [FromRoute] Guid attachmentId,
        CancellationToken ct
    )
    {
        var response = await boardService.DeleteAttachmentAsync(id, cardId, attachmentId, await this.GetCallerAsync(), ct);
        return StatusCode(response.Status, response);
    }
}
