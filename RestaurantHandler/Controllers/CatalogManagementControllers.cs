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

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<Response<CategoryDetails>>> RemoveImage(Guid id, CancellationToken ct) =>
        Success(await categories.RemoveImageAsync(id, ct));
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

    /// <summary>Groupes d'options proposés (pain, sauces, taille…), dans l'ordre d'affichage.</summary>
    [HttpPut("{id:guid}/option-groups")]
    public async Task<ActionResult<Response<ProductDetails>>> UpdateOptionGroups(Guid id, [FromBody] OptionGroupsUpdate dto) =>
        Success(await products.UpdateOptionGroupsAsync(id, dto));

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

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<Response<ProductDetails>>> RemoveImage(Guid id, CancellationToken ct) =>
        Success(await products.RemoveImageAsync(id, ct));
}

[ApiController]
[Route("manage/option-groups")]
[Produces("application/json")]
[EnableCors]
[Authorize(Roles = "Manager")]
public class OptionGroupsController(OptionGroupService groups) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Response<List<OptionGroupDetails>>>> List([FromQuery] bool includeArchived = false) =>
        Success(await groups.ListAsync(includeArchived));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> Get(Guid id) => Success(await groups.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<Response<OptionGroupDetails>>> Create([FromBody] OptionGroupWrite dto) =>
        CreatedWith(await groups.CreateAsync(dto));

    /// <summary>Met à jour le groupe et remplace ses choix, pour tous les produits qui le proposent.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> Update(Guid id, [FromBody] OptionGroupWrite dto) =>
        Success(await groups.UpdateAsync(id, dto));

    /// <summary>Archive (jamais de suppression) ; refusé tant qu'un produit le propose.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Response<object>>> Archive(Guid id)
    {
        await groups.ArchiveAsync(id);
        return Done("Groupe d'options archivé");
    }

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> SetImage(Guid id, IFormFile file, CancellationToken ct) =>
        Success(await groups.SetImageAsync(id, file, ct));

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> RemoveImage(Guid id, CancellationToken ct) =>
        Success(await groups.RemoveImageAsync(id, ct));

    [HttpPost("{id:guid}/choices/{choiceId:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> SetChoiceImage(
        Guid id,
        Guid choiceId,
        IFormFile file,
        CancellationToken ct
    ) => Success(await groups.SetChoiceImageAsync(id, choiceId, file, ct));

    [HttpDelete("{id:guid}/choices/{choiceId:guid}/image")]
    public async Task<ActionResult<Response<OptionGroupDetails>>> RemoveChoiceImage(Guid id, Guid choiceId, CancellationToken ct) =>
        Success(await groups.RemoveChoiceImageAsync(id, choiceId, ct));
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
    public async Task<ActionResult<Response<IngredientDetails>>> Update(Guid id, [FromBody] IngredientWrite dto) =>
        Success(await ingredients.UpdateAsync(id, dto));

    /// <summary>Rupture d'ingrédient (US-E03-4).</summary>
    [HttpPut("{id:guid}/availability")]
    public async Task<ActionResult<Response<IngredientDetails>>> SetAvailability(Guid id, [FromBody] AvailabilityUpdate dto) =>
        Success(await ingredients.SetAvailabilityAsync(id, dto.IsAvailable));

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Response<IngredientDetails>>> SetImage(Guid id, IFormFile file, CancellationToken ct) =>
        Success(await ingredients.SetImageAsync(id, file, ct));

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<Response<IngredientDetails>>> RemoveImage(Guid id, CancellationToken ct) =>
        Success(await ingredients.RemoveImageAsync(id, ct));

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

    [HttpDelete("{id:guid}/image")]
    public async Task<ActionResult<Response<MenuDetails>>> RemoveImage(Guid id, CancellationToken ct) =>
        Success(await menus.RemoveImageAsync(id, ct));

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
