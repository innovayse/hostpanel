using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>The printed form of a <see cref="ProvisionedDatabase"/> never carries the password.</summary>
public sealed class ProvisionedDatabaseTests
{
    /// <summary>Printing the record shows the names and leaves the password out.</summary>
    [Fact]
    public void ToString_NeverShowsThePassword()
    {
        var database = new ProvisionedDatabase("web_shop", "web_shop", "0123456789abcdef0123456789abcdef01234567");

        var text = database.ToString();

        Assert.Contains("web_shop", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0123456789abcdef", text, StringComparison.Ordinal);
    }
}
