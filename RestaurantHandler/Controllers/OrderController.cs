using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Consumes("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class OrderController(
    OrderService orderService,
    OrderAccountingService accountingService,
    MonthlyReportService reportService,
    FacturationService facturationService
) : ControllerBase
{
    [Authorize(Roles = "Student")]
    [HttpGet("student/orders/{orderId:guid}/invoice")]
    public async Task<ActionResult<Response<FileInfoResponse>>> GetOrderInvoice(Guid orderId)
    {
        var response = await facturationService.GetOrCreateInvoiceAsync(orderId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    // Détail d'une commande. Ouvert aux trois rôles : le service filtre sur
    // l'appelant (élève propriétaire / prof de la commande / admin).
    [Authorize(Roles = "Student, Teacher, Admin, SuperAdmin")]
    [HttpGet("orders/{orderId:guid}")]
    [ProducesResponseType(typeof(Response<OrderDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Response<OrderDetails>>> GetOrderById(Guid orderId)
    {
        var response = await orderService.GetOrderByIdAsync(orderId, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("student/orders")]
    public async Task<ActionResult<Response<List<OrderDetails>>>> GetOrdersByStudent(
        [FromBody] CustomTableState query
    )
    {
        var response = await orderService.GetOrdersByStudentAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/orders")]
    public async Task<ActionResult<Response<List<OrderDetails>>>> GetOrdersByTeacher(
        [FromBody] CustomTableState query
    )
    {
        var response = await orderService.GetOrdersByTeacherAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [AllowAnonymous]
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(Response<List<StatusOrderDetails>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Response<List<StatusOrderDetails>>>> GetAllOrderStatuses()
    {
        var response = await orderService.GetAllOrderStatusesAsync();
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Student")]
    [HttpGet("student/active-orders")]
    public async Task<ActionResult<Response<List<OrderDetails>>>> GetActiveOrdersForStudent(
    )
    {
        var response = await orderService.GetActiveOrderForStudentAsync(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/orders")]
    public async Task<ActionResult<Response<List<OrderDetails>>>> GetOrdersByAdmins(
        [FromBody] CustomTableState query
    )
    {
        var response = await orderService.GetOrdersByAdmins(query);
        return StatusCode(response.Status, response);
    }

    // État comptable d'une commande : payé au prof (dû + versé), remboursé, gain
    // plateforme. Admin uniquement (le gain est une donnée sensible).
    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpGet("admin/orders/{orderId:guid}/accounting")]
    [ProducesResponseType(typeof(Response<OrderAccounting>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Response<OrderAccounting>>> GetOrderAccounting(Guid orderId)
    {
        var response = await accountingService.GetOrderAccountingAsync(orderId);
        return StatusCode(response.Status, response);
    }

    // Bilan mensuel plateforme : les commandes ENCAISSÉES dans le mois (Payment.CreatedAt),
    // leur détail comptable, et les totaux du mois entier (pas seulement la page).
    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/monthly-report")]
    [ProducesResponseType(
        typeof(Response<MonthlyReport<MonthlyReportRow, MonthlyReportTotals>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status400BadRequest)]
    public async Task<
        ActionResult<Response<MonthlyReport<MonthlyReportRow, MonthlyReportTotals>>>
    > GetAdminMonthlyReport([FromBody] MonthlyReportQuery query)
    {
        var response = await reportService.GetAdminMonthlyReportAsync(query);
        return StatusCode(response.Status, response);
    }

    // POST et pas GET : l'export doit reprendre EXACTEMENT les filtres de l'écran,
    // donc le même corps de requête que la liste. En erreur on garde l'enveloppe JSON.
    [Authorize(Roles = "Admin, SuperAdmin")]
    [HttpPost("admin/monthly-report/export")]
    [Produces("text/csv", "application/json")]
    public async Task<IActionResult> ExportAdminMonthlyReport([FromBody] MonthlyReportQuery query)
    {
        var response = await reportService.ExportAdminMonthlyReportCsvAsync(query);
        if (response.Status != 200 || response.Data is null)
            return StatusCode(response.Status, response);

        return File(response.Data.Content, "text/csv", response.Data.FileName);
    }

    // Page de compta du prof : ses commandes du mois. N'expose ni les frais Stripe,
    // ni le net encaissé, ni le gain plateforme.
    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/monthly-report")]
    [ProducesResponseType(
        typeof(Response<MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Response<object>), StatusCodes.Status400BadRequest)]
    public async Task<
        ActionResult<Response<MonthlyReport<TeacherMonthlyReportRow, TeacherMonthlyReportTotals>>>
    > GetTeacherMonthlyReport([FromBody] MonthlyReportQuery query)
    {
        var response = await reportService.GetTeacherMonthlyReportAsync(query, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("teacher/monthly-report/export")]
    [Produces("text/csv", "application/json")]
    public async Task<IActionResult> ExportTeacherMonthlyReport([FromBody] MonthlyReportQuery query)
    {
        var response = await reportService.ExportTeacherMonthlyReportCsvAsync(query, await this.GetCallerAsync());
        if (response.Status != 200 || response.Data is null)
            return StatusCode(response.Status, response);

        return File(response.Data.Content, "text/csv", response.Data.FileName);
    }
}
