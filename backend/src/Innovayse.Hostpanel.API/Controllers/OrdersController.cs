using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Orders.Commands.AcceptOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.CancelOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;
using Innovayse.Hostpanel.Application.Orders.Commands.ConfirmOrderPayment;
using Innovayse.Hostpanel.Application.Orders.Commands.CreateOrderPaymentIntent;
using Innovayse.Hostpanel.Application.Orders.Commands.DeleteOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.StartOrderGatewayPayment;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Queries.GetOrder;
using Innovayse.Hostpanel.Application.Orders.Queries.ListOrders;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Endpoints for placing, viewing, and managing orders.
/// Place is open to guests and authenticated users; all other endpoints require staff.
/// </summary>
/// <remarks>
/// The four payment endpoints are the exception: they stay <c>[AllowAnonymous]</c> because a guest
/// checkout has no credential to present, and they are authorised instead by the order's payment
/// token, which the caller received when it placed the order. The route id alone never authorises
/// anything here: ids are sequential, and before the token existed anyone could start a gateway
/// session on a stranger's order and, since a live session locks the invoice for the length of the
/// gateway's session window, keep the real payer from paying at all.
/// Because they take a token, they share the small sign-in budget per caller, and placing an
/// order, which may register a guest account and send mail, takes the budget for anonymous writes.
/// </remarks>
[Route("api/orders")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class OrdersController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public OrdersController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Places a new order. Supports both authenticated clients and guest checkout.
    /// </summary>
    /// <remarks>
    /// Which account the order belongs to is decided in the handler from the credential, and
    /// the checkout IP is read there too. Neither is on the command, so this action binds the
    /// items and dispatches: there is nothing left here to get wrong.
    /// </remarks>
    /// <param name="command">The items, payment method and, for a guest, registration details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new order's id and number, its invoice id and its payment token; 400 with
    /// <c>VALIDATION_ERROR</c>, <c>ORDER_PAYMENT_METHOD_UNAVAILABLE</c>,
    /// <c>ORDER_CURRENCY_UNAVAILABLE</c>, <c>ORDER_GUEST_DETAILS_REQUIRED</c>,
    /// <c>PRODUCT_NOT_AVAILABLE</c>, <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c>,
    /// <c>ORDER_DOMAIN_PRICING_INVALID</c> or <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c>;
    /// 403 with <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the signed-in caller's account may not order, or
    /// <c>CLIENT_PERMISSION_DENIED</c> when they lack the ordering permission or the enabled identity plugin refuses them;
    /// 404 with <c>PRODUCT_NOT_FOUND</c>, or <c>DOMAIN_NOT_FOUND</c> when a renewal names a domain
    /// the client does not hold; 409 with <c>ACCOUNT_EMAIL_EXISTS</c> when a guest's address already
    /// has an account; 429 when the rate limit is hit; 500 on an unexpected fault (the invoice of an
    /// order that failed to save is cancelled); 503 with <c>ORDER_NUMBER_RANGE_EXHAUSTED</c>.
    /// </returns>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(typeof(PlaceOrderResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceAsync([FromBody] PlaceOrderCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PlaceOrderResultDto>>(command, ct);
        return HandleResult(outcome, result => CreatedResult($"/api/orders/{result.OrderId}", result));
    }

    /// <summary>Returns a paginated, optionally filtered list of all orders.</summary>
    /// <param name="query">The page, page size and status filter to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 with <c>VALIDATION_ERROR</c> for a bad paging window or filter; 401 without a session
    /// without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PaginatedList<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListOrdersQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<OrderListItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single order with full detail.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the order; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a
    /// session; 403 for a caller who is not staff; 404 with <c>ORDER_NOT_FOUND</c> when the id names no
    /// order;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<OrderDetailDto>>(new GetOrderQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Accepts a pending order, transitioning it to active, and fulfils every line the way its type asks.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the lines that could not be fulfilled (empty when every line was); 400 with <c>VALIDATION_ERROR</c> for a
    /// non-positive id; 401 without a session; 403 for a non-admin; 404 with <c>ORDER_NOT_FOUND</c> when the id names no
    /// order; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(OrderFulfillmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(new AcceptOrderCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Cancels a pending order.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when cancelled, its open invoice cancelled; 400 with <c>VALIDATION_ERROR</c> for a non-positive id,
    /// <c>ORDER_NOT_PENDING</c> when it is not pending, or <c>INVOICE_CANCEL_NOT_ALLOWED</c> / <c>INVOICE_HAS_PAYMENTS</c>
    /// when its invoice already took money and cannot be cancelled; 401 without a session; 403 for a non-admin; 404 with
    /// <c>ORDER_NOT_FOUND</c> when the id names no order;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new CancelOrderCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a pending or cancelled order.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when deleted, its open invoice cancelled; 400 with <c>VALIDATION_ERROR</c> or <c>ORDER_NOT_DELETABLE</c> when it is
    /// neither pending nor cancelled, or <c>INVOICE_CANCEL_NOT_ALLOWED</c> / <c>INVOICE_HAS_PAYMENTS</c> when its invoice already
    /// took money and cannot be cancelled; 401 without a session; 403 for a non-admin; 404 with <c>ORDER_NOT_FOUND</c> when
    /// the id names no order;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteOrderCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Creates a card gateway payment intent for an order's linked invoice.
    /// Returns the client secret needed by the frontend to confirm payment.
    /// </summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">The order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the PaymentIntent client secret; 400 with <c>VALIDATION_ERROR</c>,
    /// <c>ORDER_NOT_INVOICED</c> when the order was never billed, or <c>INVOICE_NOT_PAYABLE</c> when
    /// its invoice is not open or owes nothing; 404 with <c>ORDER_NOT_FOUND</c> when the id and token
    /// do not match an order, or with <c>INVOICE_NOT_FOUND</c>; 403 with <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or
    /// <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the order's account may not pay by card; 409 with
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway; 429 when the rate limit is hit; 502
    /// with <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> when the card gateway refuses to open the intent;
    /// 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the card gateway cannot be reached.
    /// </returns>
    [HttpPost("{id:int}/create-payment-intent")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(PaymentIntentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreatePaymentIntentAsync(
        int id, [FromBody] CreateOrderPaymentIntentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaymentIntentDto>>(command with { OrderId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Confirms a card payment for an order, marks the invoice as paid,
    /// accepts the order, and triggers service creation.
    /// </summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">The card gateway payment intent id and the order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with an empty body on success; 400 with <c>VALIDATION_ERROR</c> or
    /// <c>ORDER_NOT_INVOICED</c>; 404 with <c>ORDER_NOT_FOUND</c> when the id and token do not match an
    /// order, or with <c>INVOICE_NOT_FOUND</c>; 400 with <c>PAYMENT_INTENT_MISMATCH</c> when the intent
    /// does not pay this order's invoice; 409 with <c>PAYMENT_INTENT_ALREADY_USED</c> when its payment is
    /// already recorded, or with <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway;
    /// 502 with <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c> or
    /// <c>GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED</c> when the gateway refuses; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the gateway cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/confirm-payment")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfirmPaymentAsync(
        int id, [FromBody] ConfirmOrderPaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { OrderId = id }, ct);
        return HandleResultOk(outcome);
    }

    /// <summary>
    /// Starts a hosted-gateway payment (e.g. a hosted bank page) for an order's linked invoice.
    /// Returns the gateway URL to redirect the payer's browser to.
    /// </summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">The payment module, return URL and the order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the redirect URL; 400 with <c>VALIDATION_ERROR</c>, <c>ORDER_NOT_INVOICED</c>,
    /// <c>INVOICE_NOT_PAYABLE</c>, <c>RETURN_URL_NOT_ALLOWED</c> or <c>GATEWAY_CURRENCY_MISMATCH</c>;
    /// 403 with <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the order's account may not pay;
    /// 404 with <c>ORDER_NOT_FOUND</c> when the id and token do not match an order,
    /// <c>INVOICE_NOT_FOUND</c> or <c>CURRENCY_NOT_FOUND</c>; 409 with
    /// <c>GATEWAY_SESSION_ALREADY_STARTED</c>; 503 with <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c> when the
    /// payment module is not set up;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/gateway-payment/start")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(RedirectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartGatewayPaymentAsync(
        int id, [FromBody] StartOrderGatewayPaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RedirectDto>>(command with { OrderId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Verifies a hosted-gateway payment for an order against the gateway and, when paid,
    /// marks the invoice paid and fulfills the order.
    /// </summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">The order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the payment state (Paid, Pending or Declined) and the order's number; 400 with <c>VALIDATION_ERROR</c> or
    /// <c>ORDER_NOT_INVOICED</c>; 404 with <c>ORDER_NOT_FOUND</c> when the id and token do not match an
    /// order, <c>INVOICE_NOT_FOUND</c> or <c>GATEWAY_SESSION_NOT_FOUND</c>; 503 with
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c> when the payment module is not set up;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/gateway-payment/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(OrderGatewayCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteGatewayPaymentAsync(
        int id, [FromBody] CompleteOrderGatewayPaymentCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<OrderGatewayCompletionDto>>(command with { OrderId = id }, ct);
        return HandleResult(outcome);
    }
}
