using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Orders.Commands.AcceptOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.ApproveOrderReview;
using Innovayse.Hostpanel.Application.Orders.Commands.CancelOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.CloseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;
using Innovayse.Hostpanel.Application.Orders.Commands.DeleteOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Commands.RejectOrderReview;
using Innovayse.Hostpanel.Application.Orders.Commands.RetryOrderLine;
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
/// The two payment endpoints are the exception: they stay <c>[AllowAnonymous]</c> because a guest
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
    /// <param name="command">
    /// The items, payment method and, for a guest, registration details; <c>bundleId</c> places the order from a product bundle.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new order's id and number, its invoice id and its payment token; 400 with
    /// <c>VALIDATION_ERROR</c>, <c>ORDER_PAYMENT_METHOD_UNAVAILABLE</c>,
    /// <c>ORDER_CURRENCY_UNAVAILABLE</c>, <c>ORDER_GUEST_DETAILS_REQUIRED</c>,
    /// <c>PRODUCT_NOT_AVAILABLE</c>, <c>ORDER_PRODUCT_PRICE_UNAVAILABLE</c>,
    /// <c>ORDER_DOMAIN_PRICING_INVALID</c>, <c>PRODUCT_BUNDLE_NOT_AVAILABLE</c>, <c>ORDER_BUNDLE_ITEMS_MISMATCH</c>,
    /// <c>ORDER_BUNDLE_DOMAIN_MISMATCH</c>, <c>ORDER_BUNDLE_DOMAIN_ACTION_NOT_ALLOWED</c>,
    /// <c>ORDER_BUNDLE_DISCOUNT_CURRENCY_UNAVAILABLE</c> or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c>;
    /// 403 with <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the signed-in caller's account may not order, or
    /// <c>CLIENT_PERMISSION_DENIED</c> when they lack the ordering permission or the enabled identity plugin refuses them;
    /// 404 with <c>PRODUCT_NOT_FOUND</c>, <c>PRODUCT_BUNDLE_NOT_FOUND</c>, or <c>DOMAIN_NOT_FOUND</c> when a renewal names a domain
    /// the client does not hold; 409 with <c>ACCOUNT_EMAIL_EXISTS</c> when a guest's address already
    /// has an account; 429 when the rate limit is hit; 500 on an unexpected fault (the invoice of an
    /// order that failed to save is cancelled); 503 with <c>ORDER_NUMBER_RANGE_EXHAUSTED</c>, or with
    /// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>) when the identity plugin cannot say who the caller is.
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
    /// order; 409 with <c>ORDER_LINE_CHANGED</c> when the lines could not be claimed because the order kept changing under
    /// the run (the order is accepted, and the lines stay retryable); 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(OrderFulfillmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(new AcceptOrderCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Runs the fulfilment of one failed line of an accepted order again. A line that is already fulfilled is
    /// refused and a domain the client already holds is not registered twice.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="itemId">The failed order line; wins over any line id in a body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the lines still failed after the retry (empty when the line was fulfilled); 400 with <c>VALIDATION_ERROR</c>
    /// for a non-positive id; 401 without a session; 403 for a non-admin; 404 with <c>ORDER_NOT_FOUND</c> or
    /// <c>ORDER_ITEM_NOT_FOUND</c> when an id names nothing; 409 with <c>ORDER_LINE_ALREADY_FULFILLED</c> when the line is
    /// done already, <c>ORDER_LINE_NOT_RETRYABLE</c> when the order was not accepted, the line did not fail or was
    /// refunded, or another retry is running it, or <c>ORDER_LINE_CHANGED</c> when the line changed while the retry ran
    /// (a refund or a newer attempt overtook it) and the outcome of the retry could not be recorded; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpPost("{id:int}/items/{itemId:int}/retry")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(OrderFulfillmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryLineAsync(int id, int itemId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(new RetryOrderLineCommand(id, itemId), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Closes a partly refunded line as refunded, after staff gave back by hand what the automatic refund could not. The
    /// action is written to the client's activity log and the order's status follows its lines.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="itemId">The partly refunded order line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when the line is closed as refunded; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a
    /// session; 403 for a non-admin; 404 with <c>ORDER_NOT_FOUND</c> or <c>ORDER_ITEM_NOT_FOUND</c> when an id names
    /// nothing; 409 with <c>ORDER_LINE_REFUND_IN_PROGRESS</c> when the line's refund began less than the retry lease ago,
    /// <c>ORDER_LINE_NOT_REFUNDABLE</c> when the line is not failed, stuck refunding or partly refunded, or
    /// <c>ORDER_LINE_CHANGED</c> when another writer saved the order first; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/items/{itemId:int}/close-refund")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseLineRefundAsync(int id, int itemId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new CloseOrderLineRefundCommand(id, itemId), ct);
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

    /// <summary>
    /// Approves an order that fraud detection held for review: it returns to pending and is accepted and fulfilled the ordinary
    /// way, at once when its invoice was already paid while it waited.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when approved; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a non-admin; 404 with
    /// <c>ORDER_NOT_FOUND</c> when the id names no order; 409 with <c>ORDER_NOT_UNDER_REVIEW</c> when the order is not held for review;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/approve-review")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveReviewAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new ApproveOrderReviewCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Rejects an order that fraud detection held for review; its open invoice is cancelled and the client is mailed the reason.</summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">The reason the client is told.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when rejected; 400 with <c>VALIDATION_ERROR</c> for a non-positive id or a missing or too long reason, or
    /// <c>INVOICE_CANCEL_NOT_ALLOWED</c> / <c>INVOICE_HAS_PAYMENTS</c> when its invoice already took money and cannot be cancelled;
    /// 401 without a session; 403 for a non-admin; 404 with <c>ORDER_NOT_FOUND</c> when the id names no order; 409 with
    /// <c>ORDER_NOT_UNDER_REVIEW</c> when the order is not held for review;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/reject-review")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectReviewAsync(int id, [FromBody] RejectOrderReviewCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { OrderId = id }, ct);
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
    /// Starts a hosted-gateway payment (e.g. a hosted bank page) for an order's linked invoice.
    /// Returns the gateway URL to redirect the payer's browser to.
    /// </summary>
    /// <param name="id">Order primary key; wins over any order id in the body.</param>
    /// <param name="command">
    /// The payment module, return URL and the order's payment token, and optionally the choice to keep the payment method
    /// with the consent wording version. The address and browser kept as consent evidence are the request's own, set here
    /// over whatever the body says.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the redirect URL; 400 with <c>VALIDATION_ERROR</c> (a method to keep without the consent wording
    /// version), <c>ORDER_NOT_INVOICED</c>,
    /// <c>INVOICE_NOT_PAYABLE</c>, <c>RETURN_URL_NOT_ALLOWED</c> or <c>GATEWAY_CURRENCY_MISMATCH</c>;
    /// 401 with <c>UNAUTHORIZED</c> when a guest asks to keep the payment method, which only the signed-in owner of the invoice may;
    /// 403 with <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the order's account may not pay;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when a signed-in linked user without <c>ViewPayInvoices</c> asks to keep the
    /// payment method (paying without keeping it needs no permission);
    /// 404 with <c>ORDER_NOT_FOUND</c> when the id and token do not match an order,
    /// <c>INVOICE_NOT_FOUND</c> (also when the signed-in caller asking to keep the method is not the invoice's client),
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> (the same caller has no client record) or <c>CURRENCY_NOT_FOUND</c>; 409 with
    /// <c>GATEWAY_SESSION_ALREADY_STARTED</c>, <c>INVOICE_STORED_CHARGE_UNRESOLVED</c>, <c>INVOICE_STORED_CHARGE_HELD</c> or
    /// <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> (a method to keep at a module that cannot keep one); 503 with
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c> when the payment module is not set up, with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>
    /// when the gateway fails to register the payment or to create the customer of a method to keep, or with
    /// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin cannot say which account the signed-in caller acts for;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/gateway-payment/start")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(RedirectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartGatewayPaymentAsync(
        int id, [FromBody] StartOrderGatewayPaymentCommand command, CancellationToken ct)
    {
        // The consent evidence is the connection's and the headers', never the body's: the overlay wins over any value sent.
        var outcome = await _bus.InvokeAsync<Result<RedirectDto>>(
            command with { OrderId = id, IpAddress = RequestIpAddress, UserAgent = RequestUserAgent },
            ct);
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
