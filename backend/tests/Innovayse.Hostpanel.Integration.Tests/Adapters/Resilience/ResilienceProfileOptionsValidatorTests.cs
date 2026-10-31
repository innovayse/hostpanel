using Innovayse.Hostpanel.Infrastructure;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Resilience;

/// <summary>
/// <see cref="ResilienceProfileOptionsValidator"/> and the <see cref="HttpResilienceOptionsValidator"/> that
/// calls it: every <see cref="TimeSpan"/> bound is enforced, a total timeout below the attempt timeout is refused,
/// and a bad configured profile stops the startup validation.
/// </summary>
public sealed class ResilienceProfileOptionsValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly ResilienceProfileOptionsValidator _validator = new();

    /// <summary>Sets one named <see cref="TimeSpan"/> property on a default profile.</summary>
    /// <param name="property">The property name.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>A profile that is valid except possibly for that property.</returns>
    private static ResilienceProfileOptions With(string property, TimeSpan value)
    {
        var profile = new ResilienceProfileOptions
        {
            AttemptTimeout = TimeSpan.FromSeconds(1),
            TotalTimeout = TimeSpan.FromSeconds(30),
        };

        switch (property)
        {
            case nameof(ResilienceProfileOptions.AttemptTimeout):
                profile.AttemptTimeout = value;
                break;
            case nameof(ResilienceProfileOptions.TotalTimeout):
                profile.TotalTimeout = value;
                break;
            case nameof(ResilienceProfileOptions.RetryDelay):
                profile.RetryDelay = value;
                break;
            case nameof(ResilienceProfileOptions.SamplingDuration):
                profile.SamplingDuration = value;
                break;
            case nameof(ResilienceProfileOptions.BreakDuration):
                profile.BreakDuration = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown property.");
        }

        return profile;
    }

    /// <summary>The defaults are inside every bound.</summary>
    [Fact]
    public void Defaults_AreValid()
    {
        _validator.Validate(null, new ResilienceProfileOptions()).Succeeded.Should().BeTrue();
    }

    /// <summary>Values exactly on a bound are accepted.</summary>
    [Theory]
    [InlineData(nameof(ResilienceProfileOptions.AttemptTimeout), 100)]
    [InlineData(nameof(ResilienceProfileOptions.AttemptTimeout), 600000)]
    [InlineData(nameof(ResilienceProfileOptions.TotalTimeout), 100)]
    [InlineData(nameof(ResilienceProfileOptions.TotalTimeout), 600000)]
    [InlineData(nameof(ResilienceProfileOptions.RetryDelay), 0)]
    [InlineData(nameof(ResilienceProfileOptions.RetryDelay), 60000)]
    [InlineData(nameof(ResilienceProfileOptions.SamplingDuration), 1000)]
    [InlineData(nameof(ResilienceProfileOptions.SamplingDuration), 600000)]
    [InlineData(nameof(ResilienceProfileOptions.BreakDuration), 1000)]
    [InlineData(nameof(ResilienceProfileOptions.BreakDuration), 600000)]
    public void ValueOnABound_IsAccepted(string property, int milliseconds)
    {
        _validator.Validate(null, With(property, TimeSpan.FromMilliseconds(milliseconds))).Succeeded.Should().BeTrue();
    }

    /// <summary>A value just outside either bound is refused and the message names the property.</summary>
    [Theory]
    [InlineData(nameof(ResilienceProfileOptions.AttemptTimeout), 99)]
    [InlineData(nameof(ResilienceProfileOptions.AttemptTimeout), 600001)]
    [InlineData(nameof(ResilienceProfileOptions.TotalTimeout), 99)]
    [InlineData(nameof(ResilienceProfileOptions.TotalTimeout), 600001)]
    [InlineData(nameof(ResilienceProfileOptions.RetryDelay), -1)]
    [InlineData(nameof(ResilienceProfileOptions.RetryDelay), 60001)]
    [InlineData(nameof(ResilienceProfileOptions.SamplingDuration), 999)]
    [InlineData(nameof(ResilienceProfileOptions.SamplingDuration), 600001)]
    [InlineData(nameof(ResilienceProfileOptions.BreakDuration), 999)]
    [InlineData(nameof(ResilienceProfileOptions.BreakDuration), 600001)]
    public void ValueOutsideABound_IsRefused(string property, int milliseconds)
    {
        var result = _validator.Validate(null, With(property, TimeSpan.FromMilliseconds(milliseconds)));

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Contain($"The field {property} must be between");
    }

    /// <summary>Several bad values are all reported in one pass.</summary>
    [Fact]
    public void SeveralBadValues_AreAllReported()
    {
        var result = _validator.Validate(null, new ResilienceProfileOptions
        {
            AttemptTimeout = TimeSpan.Zero,
            BreakDuration = TimeSpan.FromHours(1),
        });

        result.Failures.Should().HaveCount(2);
    }

    /// <summary>The section validator reports an out-of-range bound with the profile's key.</summary>
    [Fact]
    public void SectionValidator_NamesTheProfileOfAnOutOfRangeBound()
    {
        var options = new HttpResilienceOptions();
        options.Plesk.RetryDelay = TimeSpan.FromHours(2);

        var result = new HttpResilienceOptionsValidator().Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("HttpResilience:Plesk").And.Contain("RetryDelay");
    }

    /// <summary>A total timeout below the attempt timeout is refused and the message names the profile.</summary>
    [Fact]
    public void TotalTimeoutBelowAttemptTimeout_IsRefused()
    {
        var options = new HttpResilienceOptions();
        options.Plesk.AttemptTimeout = TimeSpan.FromSeconds(20);
        options.Plesk.TotalTimeout = TimeSpan.FromSeconds(10);

        var result = new HttpResilienceOptionsValidator().Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("HttpResilience:Plesk").And.Contain("TotalTimeout");
    }

    /// <summary>The real registration fails fast: startup validation throws on a bad configured profile.</summary>
    [Fact]
    public void Startup_WithBadAttemptTimeout_ThrowsOptionsValidationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HttpResilienceOptions.SectionName}:Plesk:AttemptTimeout"] = "00:00:00.001",
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddHttpResilienceOptions(configuration)
            .BuildServiceProvider();

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("HttpResilience:Plesk").And.Contain("AttemptTimeout");
    }

    /// <summary>The real registration accepts a configuration without the section.</summary>
    [Fact]
    public void Startup_WithoutTheSection_Succeeds()
    {
        using var provider = new ServiceCollection()
            .AddHttpResilienceOptions(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Validation is a pure function of its input: many threads validating fresh options at the same time
    /// all get the same answer and none throws. This does not reproduce the old race, it only pins the property.
    /// </summary>
    [Fact]
    public void ValidatingFromManyThreads_NeverThrows_AndIsDeterministic()
    {
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<(bool Valid, bool Invalid)>();

        Parallel.For(0, 512, new ParallelOptions { MaxDegreeOfParallelism = 32 }, _ =>
        {
            var good = new HttpResilienceOptionsValidator().Validate(null, new HttpResilienceOptions());
            var bad = new ResilienceProfileOptionsValidator().Validate(
                null,
                new ResilienceProfileOptions { AttemptTimeout = TimeSpan.FromHours(1) });
            outcomes.Add((good.Succeeded, bad.Failed));
        });

        outcomes.Should().HaveCount(512).And.OnlyContain(o => o.Valid && o.Invalid);
    }
}
