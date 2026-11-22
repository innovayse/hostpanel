using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Domain.Tests.Provisioning;

/// <summary>The order that creates a hosting account never prints the password or the owner contact it carries.</summary>
public sealed class ProvisionAccountOrderTests
{
    /// <summary>The password and the owner's contact are masked in <c>ToString</c> and in interpolation; the other members stay readable.</summary>
    [Fact]
    public void ToString_MasksThePasswordAndTheOwnerContact()
    {
        var order = new ProvisionAccountOrder(7, "example.com", "alice", "pw-secret-1", "Basic", 3, "owner@mail.test", "Olga Owner");

        foreach (var text in new[] { order.ToString(), $"{order}" })
        {
            text.Should().NotContain("pw-secret-1").And.NotContain("owner@mail.test").And.NotContain("Olga Owner")
                .And.Contain("example.com").And.Contain("alice").And.Contain("Password = ***").And.Contain("ClientEmail = ***");
        }
    }
}
