using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Domain.Tests.Servers;

/// <summary>The credentials a control panel is reached with never show up when the value is formatted.</summary>
public sealed class ServerCredentialsTests
{
    /// <summary>Neither <c>ToString</c> nor interpolation contains the password, the API token or the access hash.</summary>
    [Fact]
    public void ToString_DoesNotContainAnyCredential()
    {
        var credentials = new ServerCredentials("pw-secret-1", "api-token-2", "access-hash-3");

        foreach (var text in new[] { credentials.ToString(), $"{credentials}" })
        {
            text.Should().NotContain("pw-secret-1").And.NotContain("api-token-2").And.NotContain("access-hash-3");
            text.Should().Contain("Password = ***").And.Contain("ApiToken = ***").And.Contain("AccessHash = ***");
        }
    }

    /// <summary>The empty credentials still print, with the absent values shown as null.</summary>
    [Fact]
    public void ToString_None_ShowsAbsentValues()
    {
        ServerCredentials.None.ToString().Should().Be("ServerCredentials { Password = null, ApiToken = null, AccessHash = null }");
    }
}
