using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Common.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="HostedAccountDiff"/>: the one pure comparison of a service's hosting account against what its panel reports. The
/// platform is the source of truth, so a status, package or password difference is something to push, the panel's address is something
/// to record, and a domain difference is only reported.
/// </summary>
public sealed class HostedAccountDiffTests
{
    /// <summary>Builds an active service with a provisioning reference, a domain and a stored password.</summary>
    /// <returns>The service.</returns>
    private static ClientService ActiveService()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, TestMoments.Seeded);
        service.AssignHostingAccount("Example.com", "alice", "stored-secret");
        service.ActivateOn(3, "alice", TestMoments.Seeded, TestMoments.Seeded);
        return service;
    }

    /// <summary>Builds a suspended service with the same account.</summary>
    /// <returns>The service.</returns>
    private static ClientService SuspendedService()
    {
        var service = ActiveService();
        service.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true);
        return service;
    }

    /// <summary>Builds what a panel reports for the account the services above hold.</summary>
    /// <param name="status">The reported status.</param>
    /// <returns>The panel's state; the domain, package and address match the service.</returns>
    private static HostedAccountState Panel(HostedAccountStatus status = HostedAccountStatus.Active)
    {
        return new HostedAccountState(status, "example.com", "Starter", "203.0.113.9");
    }

    /// <summary>Finds the entry of one field.</summary>
    /// <param name="drifts">What the comparison answered.</param>
    /// <param name="field">The field wanted.</param>
    /// <returns>The entry.</returns>
    private static HostedAccountDrift Of(IReadOnlyList<HostedAccountDrift> drifts, PanelSyncField field)
    {
        return drifts.Should().ContainSingle(d => d.Field == field).Which;
    }

    /// <summary>An account that matches on every field is in step, and the password is not part of the answer unless it is asked for.</summary>
    [Fact]
    public void Between_EverythingMatches_IsInSyncAndHasNoPasswordEntry()
    {
        var service = ActiveService();
        service.RecordAssignedIp("203.0.113.9");

        var drifts = HostedAccountDiff.Between(service, "starter", Panel(), pushPassword: false);

        drifts.Select(d => d.Field).Should().Equal(PanelSyncField.Status, PanelSyncField.Package, PanelSyncField.Domain, PanelSyncField.IpAddress);
        drifts.Should().OnlyContain(d => d.Kind == HostedAccountDriftKind.InSync);
    }

    /// <summary>An active service whose account the panel reports as suspended is a status to push, and the other way round.</summary>
    /// <param name="serviceSuspended">Whether the service is suspended.</param>
    /// <param name="panel">What the panel reports.</param>
    [Theory]
    [InlineData(false, HostedAccountStatus.Suspended)]
    [InlineData(true, HostedAccountStatus.Active)]
    public void Between_StatusDiffers_IsAStatusToPush(bool serviceSuspended, HostedAccountStatus panel)
    {
        var service = serviceSuspended ? SuspendedService() : ActiveService();

        var drift = Of(HostedAccountDiff.Between(service, "starter", Panel(panel), pushPassword: false), PanelSyncField.Status);

        drift.Kind.Should().Be(HostedAccountDriftKind.Differs);
        drift.PanelValue.Should().Be(panel.ToString());
    }

    /// <summary>A suspended service whose account is suspended is in step.</summary>
    [Fact]
    public void Between_SuspendedBothSides_IsInSync()
    {
        var drift = Of(
            HostedAccountDiff.Between(SuspendedService(), "starter", Panel(HostedAccountStatus.Suspended), pushPassword: false),
            PanelSyncField.Status);

        drift.Kind.Should().Be(HostedAccountDriftKind.InSync);
    }

    /// <summary>The package compares ignoring letter case, and another package is one to push.</summary>
    [Fact]
    public void Between_Package_IgnoresCaseAndPushesAnotherPackage()
    {
        var service = ActiveService();

        Of(HostedAccountDiff.Between(service, "STARTER", Panel(), pushPassword: false), PanelSyncField.Package)
            .Kind.Should().Be(HostedAccountDriftKind.InSync);

        var drift = Of(HostedAccountDiff.Between(service, "premium", Panel(), pushPassword: false), PanelSyncField.Package);
        drift.Kind.Should().Be(HostedAccountDriftKind.Differs);
        drift.PanelValue.Should().Be("Starter");
    }

    /// <summary>A package the product does not name, or the panel does not report, cannot be reconciled.</summary>
    /// <param name="wanted">The package of the product.</param>
    /// <param name="reported">The package the panel reports.</param>
    [Theory]
    [InlineData(null, "Starter")]
    [InlineData("  ", "Starter")]
    [InlineData("starter", null)]
    [InlineData("starter", " ")]
    public void Between_PackageUnknownOnOneSide_IsUnsupported(string? wanted, string? reported)
    {
        var panel = Panel() with { PackageName = reported };

        Of(HostedAccountDiff.Between(ActiveService(), wanted, panel, pushPassword: false), PanelSyncField.Package)
            .Kind.Should().Be(HostedAccountDriftKind.Unsupported);
    }

    /// <summary>A domain that differs is reported with the panel's value and never marked fixable; one the panel does not report is unsupported too.</summary>
    /// <param name="reported">The domain the panel reports.</param>
    [Theory]
    [InlineData("other.example.test")]
    [InlineData(null)]
    public void Between_DomainDiffers_IsUnsupportedWithThePanelsValue(string? reported)
    {
        var panel = Panel() with { Domain = reported };

        var drift = Of(HostedAccountDiff.Between(ActiveService(), "starter", panel, pushPassword: false), PanelSyncField.Domain);

        drift.Kind.Should().Be(HostedAccountDriftKind.Unsupported);
        drift.PanelValue.Should().Be(reported);
    }

    /// <summary>The same domain in another letter case is in step.</summary>
    [Fact]
    public void Between_DomainInAnotherCase_IsInSync()
    {
        var panel = Panel() with { Domain = "EXAMPLE.COM" };

        Of(HostedAccountDiff.Between(ActiveService(), "starter", panel, pushPassword: false), PanelSyncField.Domain)
            .Kind.Should().Be(HostedAccountDriftKind.InSync);
    }

    /// <summary>A valid address the service does not hold yet is one to record, in its canonical form.</summary>
    [Fact]
    public void Between_NewAddress_IsRecordedInCanonicalForm()
    {
        var panel = Panel() with { IpAddress = " 2001:0DB8:0000:0000:0000:0000:0000:0001 " };

        var drift = Of(HostedAccountDiff.Between(ActiveService(), "starter", panel, pushPassword: false), PanelSyncField.IpAddress);

        drift.Kind.Should().Be(HostedAccountDriftKind.Differs);
        drift.PanelValue.Should().Be("2001:db8::1");
    }

    /// <summary>An address written another way but meaning the one the service holds is in step.</summary>
    [Fact]
    public void Between_SameAddressWrittenAnotherWay_IsInSync()
    {
        var service = ActiveService();
        service.RecordAssignedIp("2001:db8:0:0:0:0:0:1");
        var panel = Panel() with { IpAddress = "2001:DB8::1" };

        Of(HostedAccountDiff.Between(service, "starter", panel, pushPassword: false), PanelSyncField.IpAddress)
            .Kind.Should().Be(HostedAccountDriftKind.InSync);
    }

    /// <summary>An address the panel does not report, or reports as something that is no address, is left out and never stored.</summary>
    /// <param name="reported">What the panel reports.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("1")]
    [InlineData("203.0.113.999")]
    public void Between_AddressNotUsable_IsUnsupportedWithNoValue(string? reported)
    {
        var panel = Panel() with { IpAddress = reported };

        var drift = Of(HostedAccountDiff.Between(ActiveService(), "starter", panel, pushPassword: false), PanelSyncField.IpAddress);

        drift.Kind.Should().Be(HostedAccountDriftKind.Unsupported);
        drift.PanelValue.Should().BeNull();
    }

    /// <summary>The password is an entry only when it is asked for, is pushed when the service has one, and never carries a value.</summary>
    [Fact]
    public void Between_PushPasswordWithAStoredPassword_IsAPasswordToPush()
    {
        var drift = Of(HostedAccountDiff.Between(ActiveService(), "starter", Panel(), pushPassword: true), PanelSyncField.Password);

        drift.Kind.Should().Be(HostedAccountDriftKind.Differs);
        drift.PanelValue.Should().BeNull();
    }

    /// <summary>Asking for the password to be pushed when the service holds none leaves nothing to push.</summary>
    [Fact]
    public void Between_PushPasswordWithNoStoredPassword_IsUnsupported()
    {
        var service = ClientService.Create(1, 2, BillingCycle.Monthly, TestMoments.Seeded);
        service.ActivateOn(3, "alice", TestMoments.Seeded, TestMoments.Seeded);

        Of(HostedAccountDiff.Between(service, "starter", Panel(), pushPassword: true), PanelSyncField.Password)
            .Kind.Should().Be(HostedAccountDriftKind.Unsupported);
    }
}
