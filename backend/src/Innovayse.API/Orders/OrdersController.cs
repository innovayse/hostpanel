namespace Innovayse.API.Orders;

using Innovayse.API.Billing;
using Innovayse.API.Billing.Extensions;
using Innovayse.API.Orders.Requests;
using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.AcceptOrder;
using Innovayse.Application.Orders.Commands.CancelOrder;
using Innovayse.Application.Orders.Commands.CompleteOrderGatewayPayment;
using Innovayse.Application.Orders.Commands.ConfirmOrderPayment;
using Innovayse.Application.Orders.Commands.CreateOrderPaymentIntent;
using Innovayse.Application.Orders.Commands.DeleteOrder;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Orders.Commands.StartOrderGatewayPayment;
using Innovayse.Application.Orders.Queries.GetOrder;
using Innovayse.Application.Orders.Queries.ListOrders;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Endpoints for placing, viewing, and managing orders.
/// Place is open to guests and authenticated users; all other endpoints require Admin or Reseller role.
/// </summary>
/// <remarks>
/// The four payment endpoints are the exception: they stay <c>[AllowAnonymous]</c> because a guest
/// checkout has no credential to present, and they are authorised instead by the order's payment
/// token, which the caller received when it placed the order. The route id alone never authorises
/// anything here -- ids are sequential, and before the token existed anyone could start a gateway
/// session on a stranger's order and, since a live session locks the invoice for the length of the
/// gateway's session window, keep the real payer from paying at all.
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Places a new order. Supports both authenticated clients and guest checkout.
    /// </summary>
    /// <remarks>
    /// Which account the order belongs to is decided in the handler from the credential, and
    /// the checkout IP is read there too. Neither is on the command, so this action binds the
    /// items and dispatches — there is nothing left here to get wrong.
    /// </remarks>
    /// <param name="request">Order placement request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the order and invoice IDs.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<PlaceOrderResultDto>> PlaceAsync(
        [FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new PlaceOrderItemDto(
                i.Pid, i.BillingCycle, i.Domain, i.Hostname,
                i.DomainAction, i.EppCode, i.Years))
            .ToList();

        var cmd = new PlaceOrderCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.PhoneNumber,
            request.PaymentMethod,
            items);

        var result = await bus.InvokeAsync<PlaceOrderResultDto>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Returns a paginated, optionally filtered list of all orders.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="status">Optional status filter (e.g. "Pending", "Active").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated order list.</returns>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<OrderListItemDto>>(
            new ListOrdersQuery(page, pageSize, status), ct);
        return Ok(result);
    }

    /// <summary>Returns a single order with full detail.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Order detail DTO.</returns>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
    public async Task<ActionResult<OrderDetailDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<OrderDetailDto>(new GetOrderQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Accepts a pending order, transitioning it to active.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/accept")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AcceptAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new AcceptOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Cancels a pending order.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new CancelOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Permanently deletes an order.</summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Creates a Stripe PaymentIntent for an order's linked invoice.
    /// Returns the client secret needed by the frontend to confirm payment.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="request">Request body carrying the order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the PaymentIntent client secret.</returns>
    [HttpPost("{id:int}/create-payment-intent")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePaymentIntentAsync(
        int id, [FromBody] OrderPaymentTokenRequest request, CancellationToken ct)
    {
        var clientSecret = await bus.InvokeAsync<string>(
            new CreateOrderPaymentIntentCommand(id, request.PaymentToken), ct);
        return Ok(new { clientSecret });
    }

    /// <summary>
    /// Confirms a Stripe payment for an order, marks the invoice as paid,
    /// accepts the order, and triggers service creation.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="request">Request body containing the PaymentIntent ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with success flag.</returns>
    [HttpPost("{id:int}/confirm-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPaymentAsync(
        int id, [FromBody] ConfirmPaymentRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new ConfirmOrderPaymentCommand(id, request.PaymentIntentId, request.PaymentToken), ct);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Starts a hosted-gateway payment (e.g. Inecobank) for an order's linked invoice.
    /// Returns the gateway URL to redirect the payer's browser to.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="request">The payment module, return URL and the order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the redirect URL.</returns>
    [HttpPost("{id:int}/gateway-payment/start")]
    [AllowAnonymous]
    public async Task<IActionResult> StartGatewayPaymentAsync(
        int id,
        [FromBody] StartOrderGatewayPaymentRequest request,
        CancellationToken ct)
    {
        var redirectUrl = await bus.InvokeAsync<string>(
            new StartOrderGatewayPaymentCommand(id, request.Module, request.ReturnUrl, request.PaymentToken), ct);
        return Ok(new { redirectUrl });
    }

    /// <summary>
    /// Verifies a hosted-gateway payment for an order against the gateway and, when paid,
    /// marks the invoice paid and fulfills the order.
    /// </summary>
    /// <param name="id">Order primary key.</param>
    /// <param name="request">Request body carrying the order's payment token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the payment state: paid, pending, or declined.</returns>
    [HttpPost("{id:int}/gateway-payment/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteGatewayPaymentAsync(
        int id, [FromBody] OrderPaymentTokenRequest request, CancellationToken ct)
    {
        var state = await bus.InvokeAsync<GatewayCompletionState>(
            new CompleteOrderGatewayPaymentCommand(id, request.PaymentToken), ct);

        // The only shaping left here, and it belongs here: the wire spelling of a domain enum
        // is the API's business, and the enum never leaves the backend in that form.
        return Ok(new { state = state.ToWireString() });
    }
}
