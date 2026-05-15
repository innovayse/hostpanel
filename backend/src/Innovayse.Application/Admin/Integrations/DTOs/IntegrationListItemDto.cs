namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Summary row returned by the list-integrations endpoint.
/// </summary>
/// <param name="Slug">URL-safe identifier for the integration (e.g. "stripe").</param>
/// <param name="Name">Human-readable display name (e.g. "Stripe").</param>
/// <param name="Category">Grouping label (e.g. "Payment Gateways").</param>
/// <param name="IsEnabled">Whether the integration is currently active.</param>
/// <param name="IsConfigured">Whether all required credential fields are non-empty.</param>
/// <param name="IsPlugin">True if this entry comes from an installed plugin (not built-in).</param>
public record IntegrationListItemDto(
    string Slug,
    string Name,
    string Category,
    bool IsEnabled,
    bool IsConfigured,
    bool IsPlugin = false);
