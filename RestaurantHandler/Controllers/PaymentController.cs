using RestaurantHandler.Models;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static RestaurantHandler.Services.CheckoutService;
using static RestaurantHandler.Services.StripeConnectService;

namespace RestaurantHandler.Controllers;

[Produces("application/json")]
[Route("[controller]")]
[ApiController]
[EnableCors]
public class PaymentController(
    CheckoutService checkoutService,
    StripeConnectService connectService,
    StripeWebhookService webhookService
) : ControllerBase
{
    /// <summary>
    /// Creates a Stripe Checkout Session for one of the caller's own orders and returns the URL
    /// the client should be redirected to.
    /// </summary>
    [Authorize(Roles = "Student")]
    [HttpPost("checkout")]
    public async Task<ActionResult<Response<object>>> CreateCheckout([FromBody] CheckoutRequest request)
    {
        var response = await checkoutService.CreateCheckoutAsync(request, await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Creates a Stripe Connect (Express) account and returns an onboarding URL the user
    /// should be redirected to in order to fill in their details (bank, identity, etc.).
    /// The account always belongs to the teacher of the session; the request body is ignored
    /// and only kept so existing clients keep compiling.
    /// </summary>
    [Authorize(Roles = "Teacher")]
    [HttpPost("connect/account")]
    public async Task<ActionResult<Response<object>>> CreateConnectedAccount(
        [FromBody] CreateAccountRequest? request
    )
    {
        var response = await connectService.CreateConnectedAccountAsync(await this.GetCallerAsync());
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Stripe webhook endpoint for platform events (e.g. checkout.session.completed).
    /// This is the endpoint registered with connect=false, verified with STRIPE_WEBHOOK_SECRET.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<ActionResult<Response<object>>> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var response = await webhookService.HandleWebhook(
            json,
            Request.Headers["Stripe-Signature"]!,
            EnvironmentVariables.STRIPE_WEBHOOK_SECRET
        );
        return StatusCode(response.Status, response);
    }

    /// <summary>
    /// Stripe webhook endpoint for connected-account events (e.g. account.updated).
    /// This is the endpoint registered with connect=true, verified with
    /// STRIPE_CONNECT_WEBHOOK_SECRET.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhook/connect")]
    public async Task<ActionResult<Response<object>>> ConnectWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var response = await webhookService.HandleWebhook(
            json,
            Request.Headers["Stripe-Signature"]!,
            EnvironmentVariables.STRIPE_CONNECT_WEBHOOK_SECRET
        );
        return StatusCode(response.Status, response);
    }
}
