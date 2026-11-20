using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary><see cref="ClientService.RecordHostingResourceChanged"/>: every change moves the revision, so every change writes the service row.</summary>
public sealed class ClientServiceHostingResourceRevisionTests
{
    /// <summary>A new service starts at zero and each recorded change moves the revision by one, apart from the SSH key revision.</summary>
    [Fact]
    public void RecordHostingResourceChanged_MovesOnlyItsOwnRevision()
    {
        var service = ClientService.Create(1, 1, BillingCycle.Monthly, DateTimeOffset.UnixEpoch);

        service.RecordHostingResourceChanged();
        service.RecordHostingResourceChanged();

        Assert.Equal(2, service.HostingResourceRevision);
        Assert.Equal(0, service.SshKeyRevision);
    }
}
