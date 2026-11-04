using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>The hosting account details staff correct never print the panel password.</summary>
public sealed class ServiceHostingAccountTests
{
    /// <summary>The password is masked in <c>ToString</c> and in interpolation; the domain stays readable.</summary>
    [Fact]
    public void ToString_MasksThePassword()
    {
        var account = new ServiceHostingAccount(3, "example.com", "ref-1", "203.0.113.5", "alice", "pw-secret-1");

        foreach (var text in new[] { account.ToString(), $"{account}" })
        {
            text.Should().NotContain("pw-secret-1").And.Contain("example.com").And.Contain("Password = ***");
        }
    }
}
