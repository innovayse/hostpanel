using Innovayse.Hostpanel.Application.Common.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// <see cref="StoredChargeOptions"/>: the defaults of the renewal retry policy and which values it refuses at startup.
/// </summary>
public sealed class StoredChargeOptionsTests
{
    /// <summary>The defaults wait three days between attempts and allow three attempts, and are valid.</summary>
    [Fact]
    public void Defaults_WaitThreeDaysAndAllowThreeAttempts()
    {
        var options = new StoredChargeOptions();

        options.RetryAfterDays.Should().Be(3);
        options.MaxAttemptsPerInvoice.Should().Be(3);
        options.RetryAfter.Should().Be(TimeSpan.FromDays(3));
        options.IsValid.Should().BeTrue();
    }

    /// <summary>A wait or an attempt count outside its range is refused, so a mistyped value stops the start.</summary>
    /// <param name="days">The wait in days.</param>
    /// <param name="attempts">The attempts per invoice.</param>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(StoredChargeOptions.MaxRetryAfterDaysLimit + 1, 3)]
    [InlineData(3, 0)]
    [InlineData(3, StoredChargeOptions.MaxAttemptsLimit + 1)]
    public void IsValid_OutsideTheRange_IsFalse(int days, int attempts)
    {
        var options = new StoredChargeOptions { RetryAfterDays = days, MaxAttemptsPerInvoice = attempts };

        options.IsValid.Should().BeFalse();
    }

    /// <summary>The edges of both ranges are accepted.</summary>
    /// <param name="days">The wait in days.</param>
    /// <param name="attempts">The attempts per invoice.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(StoredChargeOptions.MaxRetryAfterDaysLimit, StoredChargeOptions.MaxAttemptsLimit)]
    public void IsValid_AtTheEdges_IsTrue(int days, int attempts)
    {
        var options = new StoredChargeOptions { RetryAfterDays = days, MaxAttemptsPerInvoice = attempts };

        options.IsValid.Should().BeTrue();
    }
}
