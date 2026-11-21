using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Public list of the payment gateways a payer can pick at checkout. Which ones qualify is
/// decided by <see cref="ListAvailablePaymentMethodsHandler"/>, the same answer
/// <c>PlaceOrderHandler</c> validates an order's payment method against.
/// </summary>
[Route("api/payment-methods")]
[AllowAnonymous]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PaymentMethodsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public PaymentMethodsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists every payment gateway available at checkout right now.</summary>
    /// <param name="query">
    /// Names the currency to list for, or none for the caller's own, and the cart's products or an invoice of the caller whose
    /// product groups may restrict the methods.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the available methods, empty when none apply; 400 with <c>VALIDATION_ERROR</c> for an
    /// unusable query or <c>PAYMENT_NO_COMMON_METHOD</c> when the product groups allow no method in common;
    /// 401 when an invoice is named without a signed-in caller; 404 with <c>INVOICE_NOT_FOUND</c> when the invoice is not the caller's;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailablePaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync([FromQuery] ListAvailablePaymentMethodsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<AvailablePaymentMethodDto>>>(query, ct);
        return HandleResult(outcome);
    }
}
