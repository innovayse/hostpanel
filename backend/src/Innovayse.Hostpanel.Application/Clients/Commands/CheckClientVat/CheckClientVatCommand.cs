namespace Innovayse.Hostpanel.Application.Clients.Commands.CheckClientVat;

/// <summary>
/// Command to check a client's VAT number (their tax identification number) against the VAT number service and record the
/// answer on the client. Sent by staff from the client page, by the client's own number or country change, and by the
/// scheduled re-check.
/// </summary>
/// <param name="ClientId">The client whose number is checked.</param>
public sealed record CheckClientVatCommand(int ClientId);
