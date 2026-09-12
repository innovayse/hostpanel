namespace Innovayse.API.Billing;

using Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Public list of the payment gateways a payer can pick at checkout. Which ones qualify is
/// decided by <see cref="ListAvailablePaymentMethodsHandler"/>, the same answer
/// <c>PlaceOrderHandler</c> validates an order's payment method against.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/payment-methods")]
[AllowAnonymous]
public sealed class PaymentMethodsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Lists all active payment gateways available at checkout.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Payment methods as <c>{ module, displayname }</c> objects — the shape the checkout reads.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var methods = await bus.InvokeAsync<IReadOnlyList<AvailablePaymentMethodDto>>(
            new ListAvailablePaymentMethodsQuery(), ct);

        // The checkout reads lower-case `module` / `displayname`; the DTO is projected here
        // rather than renamed so its properties keep the C# casing every other DTO has.
        return Ok(methods.Select(m => new { module = m.Module, displayname = m.DisplayName }));
    }
}
