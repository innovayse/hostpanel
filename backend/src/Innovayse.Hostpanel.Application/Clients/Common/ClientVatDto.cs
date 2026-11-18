using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>The last completed check of a client's VAT number (their tax identification number), as staff and the client read it.</summary>
/// <param name="Status">Valid or invalid; null when the number was never checked or was changed since.</param>
/// <param name="CheckedAt">When the check was made (UTC), null while <paramref name="Status"/> is null.</param>
/// <param name="Name">The registered name the service returned for a valid number, or null.</param>
/// <param name="Address">The registered address the service returned for a valid number, or null.</param>
public sealed record ClientVatDto(VatCheckOutcome? Status, DateTimeOffset? CheckedAt, string? Name, string? Address);
