namespace Innovayse.API.Orders;

using System.Security.Claims;
using Innovayse.API.Orders.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Orders.Commands.AcceptOrder;
using Innovayse.Application.Orders.Commands.CancelOrder;
using Innovayse.Application.Orders.Commands.ConfirmOrderPayment;
using Innovayse.Application.Orders.Commands.DeleteOrder;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Orders.DTOs;
using Innovayse.Application.Orders.Queries.GetOrder;
using Innovayse.Application.Orders.Queries.ListOrders;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Endpoints for placing, viewing, and managing orders.
/// Place is open to guests and authenticated users; all other endpoints require Admin or Reseller role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="clientRepo">Client repository for resolving authenticated user's client ID.</param>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IMessageBus bus, IClientRepository clientRepo) : ControllerBase
{
    /// <summary>
    /// Places a new order. Supports both authenticated clients and guest checkout.
    /// When the caller is authenticated, the client ID is resolved from the JWT;
    /// otherwise guest registration details from the request body are used.
    /// </summary>
    /// <param name="request">Order placement request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the order and invoice IDs.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<PlaceOrderResultDto>> PlaceAsync(
        [FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        int? clientId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (userId is not null)
            {
                var client = await clientRepo.FindByUserIdAsync(userId, ct);
                clientId = client?.Id;
            }
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var items = request.Items
            .Select(i => new PlaceOrderItemDto(i.Pid, i.BillingCycle, i.Domain, i.Hostname))
            .ToList();

        var cmd = new PlaceOrderCommand(
            clientId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.PhoneNumber,
            request.PaymentMethod,
            items,
            ipAddress);

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
    /// <param name="stripeService">Stripe payment service.</param>
    /// <param name="orderRepo">Order repository.</param>
    /// <param name="invoiceRepo">Invoice repository.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the PaymentIntent client secret.</returns>
    [HttpPost("{id:int}/create-payment-intent")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePaymentIntentAsync(
        int id,
        [FromServices] IStripeService stripeService,
        [FromServices] IOrderRepository orderRepo,
        [FromServices] IInvoiceRepository invoiceRepo,
        CancellationToken ct)
    {
        var order = await orderRepo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Order {id} not found.");

        if (order.InvoiceId is null)
        {
            throw new InvalidOperationException($"Order {id} has no linked invoice.");
        }

        var invoice = await invoiceRepo.FindByIdAsync(order.InvoiceId.Value, ct)
            ?? throw new InvalidOperationException($"Invoice {order.InvoiceId} not found.");

        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(),
            ["invoiceId"] = invoice.Id.ToString(),
            ["clientId"] = order.ClientId.ToString(),
        };

        var clientSecret = await stripeService.CreatePaymentIntentAsync(invoice.Total, "usd", metadata, ct);
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
        await bus.InvokeAsync(new ConfirmOrderPaymentCommand(id, request.PaymentIntentId), ct);
        return Ok(new { success = true });
    }
}
