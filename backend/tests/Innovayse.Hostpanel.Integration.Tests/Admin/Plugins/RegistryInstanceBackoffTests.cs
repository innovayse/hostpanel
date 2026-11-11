using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>The pause between registration attempts: short at first, doubling with each failure in a row, never longer than an hour.</summary>
public sealed class RegistryInstanceBackoffTests
{
    /// <summary>The first failures wait 30 seconds, 1, 2 and 4 minutes.</summary>
    /// <param name="failures">How many attempts failed in a row.</param>
    /// <param name="seconds">The expected pause in seconds.</param>
    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(7, 1920)]
    public void ThePause_DoublesWithEveryFailure(int failures, int seconds)
    {
        RegistryInstanceBackoff.After(failures).Should().Be(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>However many attempts failed, the pause stops at one hour.</summary>
    /// <param name="failures">How many attempts failed in a row.</param>
    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void ThePause_NeverPassesOneHour(int failures)
    {
        RegistryInstanceBackoff.After(failures).Should().Be(TimeSpan.FromHours(1));
    }

    /// <summary>A count below one reads as the first failure.</summary>
    [Fact]
    public void ACountBelowOne_ReadsAsTheFirstFailure()
    {
        RegistryInstanceBackoff.After(0).Should().Be(RegistryInstanceBackoff.First);
    }
}
