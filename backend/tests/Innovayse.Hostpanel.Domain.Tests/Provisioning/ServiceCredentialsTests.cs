using Innovayse.Hostpanel.Domain.Provisioning;

namespace Innovayse.Hostpanel.Domain.Tests.Provisioning;

/// <summary>The credentials handed back for a hosting account never print the password.</summary>
public sealed class ServiceCredentialsTests
{
    /// <summary>The password is masked in <c>ToString</c> and in interpolation; the panel address stays readable.</summary>
    [Fact]
    public void ToString_MasksThePassword()
    {
        var credentials = new ServiceCredentials("alice", "pw-secret-1", "example.com", "203.0.113.5", "https://panel.example.com");

        foreach (var text in new[] { credentials.ToString(), $"{credentials}" })
        {
            text.Should().NotContain("pw-secret-1").And.Contain("alice").And.Contain("https://panel.example.com").And.Contain("Password = ***");
        }
    }
}
