using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>DTO representing a plan change request with enriched client and product data (admin view).</summary>
/// <param name="Id">Primary key of the plan change request.</param>
/// <param name="ServiceId">FK to the service whose plan changes.</param>
/// <param name="ClientId">FK to the client that owns the service.</param>
/// <param name="ClientName">Full name of that client.</param>
/// <param name="FromProductId">FK to the product the service ran on when the request was made.</param>
/// <param name="FromProductName">Display name of the product the service ran on.</param>
/// <param name="ToProductId">FK to the product the service moves to.</param>
/// <param name="ToProductName">Display name of the product the service moves to.</param>
/// <param name="Type">Whether the move is an upgrade or a downgrade. Sent as the member name, not display text.</param>
/// <param name="Status">Where the request stands. Sent as the member name, for the same reason as <paramref name="Type"/>.</param>
/// <param name="ProrationAmount">The prorated charge of an upgrade or credit of a downgrade, in <paramref name="CurrencyCode"/>.</param>
/// <param name="CurrencyCode">ISO 4217 alpha code of the client's billing currency.</param>
/// <param name="NewRecurringAmount">The new plan's recurring price per cycle, in <paramref name="CurrencyCode"/>.</param>
/// <param name="InvoiceId">The proration invoice of an upgrade, or <see langword="null"/> when there is none.</param>
/// <param name="Note">The staff note given on approval or rejection, or <see langword="null"/>.</param>
/// <param name="CreatedAt">UTC timestamp when the request was made.</param>
/// <param name="ResolvedAt">UTC timestamp when the request was completed or rejected, or <see langword="null"/> while it is open.</param>
public sealed record ServiceChangeRequestDto(
    int Id,
    int ServiceId,
    int ClientId,
    string ClientName,
    int FromProductId,
    string FromProductName,
    int ToProductId,
    string ToProductName,
    ServiceChangeType Type,
    ServiceChangeStatus Status,
    decimal ProrationAmount,
    string CurrencyCode,
    decimal NewRecurringAmount,
    int? InvoiceId,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
