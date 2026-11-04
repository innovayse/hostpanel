using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The columns of one client service the plugin host API shares; nothing else of the row leaves the query.</summary>
/// <param name="Id">Client service primary key.</param>
/// <param name="ProductId">The product the service was ordered from.</param>
/// <param name="Status">The service's lifecycle status, mapped to the SDK's by name before it reaches a plugin.</param>
/// <param name="Domain">The domain the service runs under, or <see langword="null"/> when it has none.</param>
/// <param name="CreatedAt">When the service was created (UTC); the list is ordered by it, newest first.</param>
/// <param name="NextRenewalAt">The next renewal moment (UTC), or <see langword="null"/> when the service does not renew.</param>
public sealed record PluginHostServiceDto(
    int Id, int ProductId, ServiceStatus Status, string? Domain, DateTimeOffset CreatedAt, DateTimeOffset? NextRenewalAt);
