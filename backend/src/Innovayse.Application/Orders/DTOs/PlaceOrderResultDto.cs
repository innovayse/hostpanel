namespace Innovayse.Application.Orders.DTOs;

/// <summary>Result DTO returned after successfully placing an order.</summary>
/// <param name="OrderId">The newly created order's primary key.</param>
/// <param name="InvoiceId">The newly created invoice's primary key.</param>
public record PlaceOrderResultDto(int OrderId, int InvoiceId);
