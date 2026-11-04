using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Domain.Tests.Provisioning;

/// <summary>The order that creates a hosting account never prints the password it carries.</summary>
public sealed class ProvisionAccountOrderTests
{
    /// <summary>The password is masked in <c>ToString</c> and in interpolation; the other members stay readable.</summary>
    [Fact]
    public void ToString_MasksThePassword()
    {
        var order = new ProvisionAccountOrder(7, "example.com", "alice", "pw-secret-1", "Basic");

        foreach (var text in new[] { order.ToString(), $"{order}" })
        {
            text.Should().NotContain("pw-secret-1").And.Contain("example.com").And.Contain("alice").And.Contain("Password = ***");
        }
    }
}
