namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>
/// What is held about how the client pays, as the <c>payMethods</c> section of a client data export
/// lists it. Card details themselves are kept by the card gateway, not here.
/// </summary>
/// <param name="PaymentMethod">The payment method the account prefers, when one is set.</param>
/// <param name="HasSavedCard">Whether a payment module keeps a customer for the client, so cards may be saved there.</param>
public sealed record ClientExportPayMethodDto(string? PaymentMethod, bool HasSavedCard);
