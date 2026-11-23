using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>
/// The one comparison of a service's hosting account against what its panel reports. Pure: it reads the two sides and says what
/// differs; it neither calls the panel nor changes the service. The platform is the source of truth, so a difference in the status, the
/// package or the password is a change to push to the panel, the panel's address is only recorded, and a domain that differs is
/// reported and left alone because no operation moves an account to another domain.
/// </summary>
public static class HostedAccountDiff
{
    /// <summary>Compares the account the service holds with what its panel reports.</summary>
    /// <param name="service">The service whose account is compared, Active or Suspended.</param>
    /// <param name="wantedPackage">The package of the service's product, or <see langword="null"/> when the product names none.</param>
    /// <param name="panel">What the panel reports.</param>
    /// <param name="pushPassword">Whether the stored password is to be pushed to the panel.</param>
    /// <returns>
    /// One entry for the status, the package, the domain and the address, in that order, and one for the password when
    /// <paramref name="pushPassword"/> is set.
    /// </returns>
    public static IReadOnlyList<HostedAccountDrift> Between(
        ClientService service, string? wantedPackage, HostedAccountState panel, bool pushPassword)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(panel);

        List<HostedAccountDrift> drifts =
        [
            CompareStatus(service, panel),
            ComparePackage(wantedPackage, panel),
            CompareDomain(service, panel),
            CompareIpAddress(service, panel),
        ];

        if (pushPassword)
        {
            // A panel never says what an account's password is, so there is nothing to compare: with a stored password the host's
            // value is pushed, and without one there is nothing to push.
            var kind = string.IsNullOrEmpty(service.Password) ? HostedAccountDriftKind.Unsupported : HostedAccountDriftKind.Differs;
            drifts.Add(new HostedAccountDrift(PanelSyncField.Password, kind, null));
        }

        return drifts;
    }

    /// <summary>Compares whether the account is active on the panel with the service's own status.</summary>
    /// <param name="service">The service, Active or Suspended.</param>
    /// <param name="panel">What the panel reports.</param>
    /// <returns>The status entry.</returns>
    private static HostedAccountDrift CompareStatus(ClientService service, HostedAccountState panel)
    {
        var wanted = service.Status is ServiceStatus.Suspended ? HostedAccountStatus.Suspended : HostedAccountStatus.Active;
        var kind = panel.Status == wanted ? HostedAccountDriftKind.InSync : HostedAccountDriftKind.Differs;
        return new HostedAccountDrift(PanelSyncField.Status, kind, panel.Status.ToString());
    }

    /// <summary>Compares the package the panel reports with the product's, ignoring letter case.</summary>
    /// <param name="wantedPackage">The package of the service's product, or <see langword="null"/>.</param>
    /// <param name="panel">What the panel reports.</param>
    /// <returns>The package entry.</returns>
    private static HostedAccountDrift ComparePackage(string? wantedPackage, HostedAccountState panel)
    {
        if (string.IsNullOrWhiteSpace(panel.PackageName) || string.IsNullOrWhiteSpace(wantedPackage))
        {
            return new HostedAccountDrift(PanelSyncField.Package, HostedAccountDriftKind.Unsupported, panel.PackageName);
        }

        var kind = SameText(panel.PackageName, wantedPackage) ? HostedAccountDriftKind.InSync : HostedAccountDriftKind.Differs;
        return new HostedAccountDrift(PanelSyncField.Package, kind, panel.PackageName);
    }

    /// <summary>Compares the domain the panel reports with the service's, ignoring letter case.</summary>
    /// <param name="service">The service.</param>
    /// <param name="panel">What the panel reports.</param>
    /// <returns>The domain entry; a difference is never fixable.</returns>
    private static HostedAccountDrift CompareDomain(ClientService service, HostedAccountState panel)
    {
        var kind = !string.IsNullOrWhiteSpace(panel.Domain) && SameText(panel.Domain, service.Domain)
            ? HostedAccountDriftKind.InSync
            : HostedAccountDriftKind.Unsupported;
        return new HostedAccountDrift(PanelSyncField.Domain, kind, panel.Domain);
    }

    /// <summary>Compares the address the panel reports with the one the service holds.</summary>
    /// <param name="service">The service.</param>
    /// <param name="panel">What the panel reports.</param>
    /// <returns>The address entry; a valid address that differs is recorded, anything else the panel sends is left out.</returns>
    private static HostedAccountDrift CompareIpAddress(ClientService service, HostedAccountState panel)
    {
        var address = PanelIpAddress.Normalize(panel.IpAddress);
        if (address is null)
        {
            return new HostedAccountDrift(PanelSyncField.IpAddress, HostedAccountDriftKind.Unsupported, null);
        }

        var kind = string.Equals(address, PanelIpAddress.Normalize(service.DedicatedIp), StringComparison.Ordinal)
            ? HostedAccountDriftKind.InSync
            : HostedAccountDriftKind.Differs;
        return new HostedAccountDrift(PanelSyncField.IpAddress, kind, address);
    }

    /// <summary>Compares two names the way a panel does, ignoring letter case and surrounding white space.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when they are the same name.</returns>
    private static bool SameText(string left, string? right)
    {
        return string.Equals(left.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
