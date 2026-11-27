using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Auth;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// Every column the model stores encrypted. <see cref="AppDbContext"/> gives each one the
/// encryption converter and a width sized for the encrypted form, so a credential cannot reach the
/// database in plain text because a configuration forgot the converter. A new secret column is
/// added here and nowhere else.
/// </summary>
public static class ProtectedColumns
{
    /// <summary>Gets the protected columns.</summary>
    public static IReadOnlyList<ProtectedColumn> All { get; } =
    [
        new(typeof(Server), nameof(Server.Password), ProtectedValueLimits.ServerPassword),
        new(typeof(Server), nameof(Server.ApiToken), ProtectedValueLimits.ServerApiToken),
        new(typeof(Server), nameof(Server.AccessHash), ProtectedValueLimits.ServerAccessHash),
        new(typeof(ServerSshSettings), nameof(ServerSshSettings.AgentPrivateKey), ProtectedValueLimits.AgentPrivateKey),
        new(typeof(ClientService), nameof(ClientService.Password), ProtectedValueLimits.ServicePassword),
        new(typeof(AppUser), nameof(AppUser.TwoFactorSecret), ProtectedValueLimits.TwoFactorSecret),
        new(typeof(AppUser), nameof(AppUser.PendingTwoFactorSecret), ProtectedValueLimits.TwoFactorSecret),
        new(typeof(OrderItem), nameof(OrderItem.ExtendedAttributesJson), ProtectedValueLimits.DomainExtendedAttributes),
        new(typeof(RegisteredDomain), nameof(RegisteredDomain.ExtendedAttributesJson), ProtectedValueLimits.DomainExtendedAttributes),
    ];
}
