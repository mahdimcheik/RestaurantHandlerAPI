using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RestaurantHandler.Models;
using RestaurantHandler.Services;
using static RestaurantHandler.Controllers.ApiResponses;

namespace RestaurantHandler.Controllers;

[ApiController]
[Route("manage/categories")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Response<List<CategoryDetails>>>> List([FromQuery] bool includeArchived = false) =>
        Success(await categories.ListAsync(includeArchived));

    [HttpPost]
    public async Task<ActionResult<Response<CategoryDetails>>> Create([FromBody] CategoryWrite dto) =>
        CreatedWith(await categories.CreateAsync(dto));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<CategoryDetails>>> Update(Guid id, [FromBody] CategoryWrite dto) =>
        Success(await categories.UpdateAsync(id, dto));

    [HttpPut("reorder")]
    public async Task<ActionResult<Response<List<CategoryDetails>>>> Reorder([FromBody] ReorderRequest dto) =>
        Success(await categories.ReorderAsync(dto));

    /// <summary>Archive (jamais de suppression) ; refusé si des produits actifs y sont rangés.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Archive(Guid id)
    {
        await categories.ArchiveAsync(id);
        return Done("Catégorie archivée");
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<CategoryDetails>>> SetImage(Guid id, IFormFile file, CancellationToken ct) =>
        Success(await categories.SetImageAsync(id, file, ct));
}

[ApiController]
[Route("manage/products")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Response<List<ProductDetails>>>> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] bool includeArchived = false
    ) => Success(await products.ListAsync(categoryId, includeArchived));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<ProductDetails>>> Get(Guid id) => Success(await products.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<Response<ProductDetails>>> Create([FromBody] ProductWrite dto) =>
        CreatedWith(await products.CreateAsync(dto));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<ProductDetails>>> Update(Guid id, [FromBody] ProductWrite dto) =>
        Success(await products.UpdateAsync(id, dto));

    /// <summary>Ordre des produits d'une catégorie.</summary>
    [HttpPut("reorder")]
    public async Task<ActionResult<Response<List<ProductDetails>>>> Reorder(
        [FromQuery] Guid categoryId,
        [FromBody] ReorderRequest dto
    ) => Success(await products.ReorderAsync(categoryId, dto));

    /// <summary>Rupture du produit lui-même (US-E03-4).</summary>
    [HttpPut("{id:guid}/availability")]
    public async Task<ActionResult<Response<ProductDetails>>> SetAvailability(Guid id, [FromBody] AvailabilityUpdate dto) =>
        Success(await products.SetAvailabilityAsync(id, dto.IsAvailable));

    /// <summary>Remplace toute la composition : ingrédients, remplacements, suppléments.</summary>
    [HttpPut("{id:guid}/composition")]
    public async Task<ActionResult<Response<ProductDetails>>> UpdateComposition(Guid id, [FromBody] CompositionUpdate dto) =>
        Success(await products.UpdateCompositionAsync(id, dto));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Archive(Guid id)
    {
        await products.ArchiveAsync(id);
        return Done("Produit archivé");
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<ProductDetails>>> SetImage(Guid id, IFormFile file, CancellationToken ct) =>
        Success(await products.SetImageAsync(id, file, ct));
}

[ApiController]
[Route("manage/ingredients")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class IngredientsController(IngredientService ingredients) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Response<List<IngredientDetails>>>> List([FromQuery] bool includeArchived = false) =>
        Success(await ingredients.ListAsync(includeArchived));

    [HttpPost]
    public async Task<ActionResult<Response<IngredientDetails>>> Create([FromBody] IngredientWrite dto) =>
        CreatedWith(await ingredients.CreateAsync(dto));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<IngredientDetails>>> Rename(Guid id, [FromBody] IngredientWrite dto) =>
        Success(await ingredients.RenameAsync(id, dto));

    /// <summary>Rupture d'ingrédient (US-E03-4).</summary>
    [HttpPut("{id:guid}/availability")]
    public async Task<ActionResult<Response<IngredientDetails>>> SetAvailability(Guid id, [FromBody] AvailabilityUpdate dto) =>
        Success(await ingredients.SetAvailabilityAsync(id, dto.IsAvailable));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Archive(Guid id)
    {
        await ingredients.ArchiveAsync(id);
        return Done("Ingrédient archivé");
    }
}

[ApiController]
[Route("manage/menus")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class MenusController(MenuService menus) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Response<List<MenuDetails>>>> List([FromQuery] bool includeArchived = false) =>
        Success(await menus.ListAsync(includeArchived));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<MenuDetails>>> Get(Guid id) => Success(await menus.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<Response<MenuDetails>>> Create([FromBody] MenuWrite dto) =>
        CreatedWith(await menus.CreateAsync(dto));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<MenuDetails>>> Update(Guid id, [FromBody] MenuWrite dto) =>
        Success(await menus.UpdateAsync(id, dto));

    [HttpPut("reorder")]
    public async Task<ActionResult<Response<List<MenuDetails>>>> Reorder([FromBody] ReorderRequest dto) =>
        Success(await menus.ReorderAsync(dto));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Archive(Guid id)
    {
        await menus.ArchiveAsync(id);
        return Done("Menu archivé");
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<MenuDetails>>> SetImage(Guid id, IFormFile file, CancellationToken ct) =>
        Success(await menus.SetImageAsync(id, file, ct));

    [HttpPost("{id:guid}/slots")]
    public async Task<ActionResult<Response<MenuDetails>>> AddSlot(Guid id, [FromBody] MenuSlotWrite dto) =>
        CreatedWith(await menus.AddSlotAsync(id, dto));

    [HttpPut("{id:guid}/slots/{slotId:guid}")]
    public async Task<ActionResult<Response<MenuDetails>>> UpdateSlot(Guid id, Guid slotId, [FromBody] MenuSlotWrite dto) =>
        Success(await menus.UpdateSlotAsync(id, slotId, dto));

    [HttpDelete("{id:guid}/slots/{slotId:guid}")]
    public async Task<ActionResult<Response<MenuDetails>>> ArchiveSlot(Guid id, Guid slotId) =>
        Success(await menus.ArchiveSlotAsync(id, slotId));

    [HttpPut("{id:guid}/slots/reorder")]
    public async Task<ActionResult<Response<MenuDetails>>> ReorderSlots(Guid id, [FromBody] ReorderRequest dto) =>
        Success(await menus.ReorderSlotsAsync(id, dto));

    /// <summary>Remplace les produits proposés dans un emplacement.</summary>
    [HttpPut("{id:guid}/slots/{slotId:guid}/options")]
    public async Task<ActionResult<Response<MenuDetails>>> SetSlotOptions(Guid id, Guid slotId, [FromBody] SlotOptionsUpdate dto) =>
        Success(await menus.SetSlotOptionsAsync(id, slotId, dto));
}
