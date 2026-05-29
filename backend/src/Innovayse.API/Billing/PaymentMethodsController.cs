namespace Innovayse.API.Billing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Returns available payment gateways for the checkout flow.
/// </summary>
[ApiController]
[Route("api/payment-methods")]
[AllowAnonymous]
public sealed class PaymentMethodsController : ControllerBase
{
    /// <summary>
    /// Lists all active payment gateways available at checkout.
    /// </summary>
    /// <returns>Array of payment method objects with module name and display name.</returns>
    [HttpGet]
    public IActionResult List()
    {
        var methods = new[]
        {
            new { module = "stripe", displayname = "Credit/Debit Card (Stripe)" },
            new { module = "bank_transfer", displayname = "Bank Transfer" },
        };

        return Ok(methods);
    }
}
