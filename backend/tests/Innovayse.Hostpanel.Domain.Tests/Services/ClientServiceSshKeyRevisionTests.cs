using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary><see cref="ClientService.RecordSshKeyAdded"/>: every add moves the revision, so every add writes the service row.</summary>
public sealed class ClientServiceSshKeyRevisionTests
{
    /// <summary>A new service starts at zero and each recorded add moves the revision by one.</summary>
    [Fact]
    public void RecordSshKeyAdded_MovesTheRevision()
    {
        var service = ClientService.Create(1, 1, BillingCycle.Monthly, DateTimeOffset.UnixEpoch);

        service.RecordSshKeyAdded();
        service.RecordSshKeyAdded();

        Assert.Equal(2, service.SshKeyRevision);
    }
}
