using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="DemoOptionsValidator"/>: a disabled demo is always valid; an enabled one needs a
/// positive interval, two distinct email addresses and two passwords the identity store accepts,
/// and the failure names every offending key.
/// </summary>
public sealed class DemoOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly DemoOptionsValidator _validator = new();

    /// <summary>A complete, valid, enabled option set.</summary>
    /// <returns>The options.</returns>
    private static DemoOptions Valid() => new()
    {
        Enabled = true,
        ResetIntervalHours = 6,
        AdminEmail = "admin@demo.example",
        AdminPassword = "Admin-Pass-1",
        ClientEmail = "client@demo.example",
        ClientPassword = "Client-Pass-1",
    };

    /// <summary>A disabled demo needs no other value.</summary>
    [Fact]
    public void Disabled_IsValidWhateverElseIsMissing()
    {
        _validator.Validate(null, new DemoOptions()).Succeeded.Should().BeTrue();
    }

    /// <summary>A complete enabled set passes.</summary>
    [Fact]
    public void Enabled_WithEveryValue_IsValid()
    {
        _validator.Validate(null, Valid()).Succeeded.Should().BeTrue();
    }

    /// <summary>An unset or non-positive interval is refused.</summary>
    /// <param name="hours">The interval in hours.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Enabled_WithoutAPositiveInterval_IsRefused(int hours)
    {
        var result = _validator.Validate(null, new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = hours,
            AdminEmail = "admin@demo.example",
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "client@demo.example",
            ClientPassword = "Client-Pass-1",
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(DemoOptions.ResetIntervalHours));
    }

    /// <summary>A missing or malformed email is refused by name.</summary>
    /// <param name="email">The e-mail value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void Enabled_WithABadEmail_IsRefused(string? email)
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = email,
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "client@demo.example",
            ClientPassword = "Client-Pass-1",
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(DemoOptions.AdminEmail));
    }

    /// <summary>A missing or too-short password is refused by name.</summary>
    /// <param name="password">The password value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void Enabled_WithABadPassword_IsRefused(string? password)
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = "admin@demo.example",
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "client@demo.example",
            ClientPassword = password,
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(DemoOptions.ClientPassword));
    }

    /// <summary>The two published accounts cannot share an email.</summary>
    [Fact]
    public void Enabled_WithTheSameEmailTwice_IsRefused()
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = "same@demo.example",
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "SAME@demo.example",
            ClientPassword = "Client-Pass-1",
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle()
            .Which.Should().Contain($"{nameof(DemoOptions.AdminEmail)} and {nameof(DemoOptions.ClientEmail)} must differ");
    }

    /// <summary>A password Identity would reject (no upper-case or no lower-case letter) is refused by name.</summary>
    /// <param name="password">The rejected password.</param>
    [Theory]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    public void Enabled_WithAPasswordMissingACase_IsRefused(string password)
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = "admin@demo.example",
            AdminPassword = password,
            ClientEmail = "client@demo.example",
            ClientPassword = "Client-Pass-1",
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(DemoOptions.AdminPassword));
    }

    /// <summary>Several problems are all reported in one failure.</summary>
    [Fact]
    public void Enabled_WithNothingSet_NamesEveryKey()
    {
        var result = _validator.Validate(null, new DemoOptions { Enabled = true });

        result.Failures.Should().HaveCount(5);
        var message = string.Join(' ', result.Failures!);
        message.Should().Contain(nameof(DemoOptions.ResetIntervalHours))
            .And.Contain(nameof(DemoOptions.AdminEmail))
            .And.Contain(nameof(DemoOptions.ClientEmail))
            .And.Contain(nameof(DemoOptions.AdminPassword))
            .And.Contain(nameof(DemoOptions.ClientPassword));
    }

    /// <summary>The optional contact pair: empty or a real address passes.</summary>
    /// <param name="email">The configured contact email.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contact@demo.example")]
    public void Enabled_WithAnEmptyOrValidContactEmail_IsValid(string? email)
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = "admin@demo.example",
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "client@demo.example",
            ClientPassword = "Client-Pass-1",
            ContactEmail = email,
            ContactPhone = "+1 555 0199",
        };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    /// <summary>A contact email that is set but is not an address is refused by name.</summary>
    [Fact]
    public void Enabled_WithAMalformedContactEmail_IsRefused()
    {
        var options = new DemoOptions
        {
            Enabled = true,
            ResetIntervalHours = 6,
            AdminEmail = "admin@demo.example",
            AdminPassword = "Admin-Pass-1",
            ClientEmail = "client@demo.example",
            ClientPassword = "Client-Pass-1",
            ContactEmail = "not-an-address",
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Contain(nameof(DemoOptions.ContactEmail));
    }
}
