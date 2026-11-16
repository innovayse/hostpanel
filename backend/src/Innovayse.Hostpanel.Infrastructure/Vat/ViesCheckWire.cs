namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The part of the EU VAT number service's answer that the check reads.</summary>
/// <param name="Valid">Whether the number is registered; null when the service gave no verdict.</param>
/// <param name="Name">The registered name, or a placeholder dash string when the member state does not disclose it.</param>
/// <param name="Address">The registered address, or a placeholder dash string when the member state does not disclose it.</param>
/// <param name="UserError">The service's own verdict code: <c>VALID</c>, <c>INVALID</c>, <c>INVALID_INPUT</c>, or one that says a member state or the service could not answer.</param>
internal sealed record ViesCheckWire(bool? Valid, string? Name, string? Address, string? UserError);
