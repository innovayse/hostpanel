using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CorsOptions = Innovayse.Hostpanel.Shared.Settings.CorsOptions;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The one <c>Cors</c> section feeds both the CORS policy and the gateway return URL check: it is
/// validated on start, and a blank entry (an optional slot a deployment left unset) is skipped
/// rather than failing startup or reaching the policy.
/// </summary>
public sealed class CorsOptionsTests : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The origin the Testing tier configures.</summary>
    private const string TestOrigin = "https://client.test";

    /// <summary>The host booted with the Testing configuration.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="CorsOptionsTests"/> class.</summary>
    /// <param name="factory">The shared test host.</param>
    public CorsOptionsTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>The bound origins are exactly the Testing tier's <c>Cors:AllowedOrigins</c>.</summary>
    [Fact]
    public void Origins_AreBoundFromTheCorsSection()
    {
        var options = _factory.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

        CorsOptions.SectionName.Should().Be("Cors");
        options.Origins.Should().BeEquivalentTo([TestOrigin]);
    }

    /// <summary>A host with a blank origin entry starts, and neither the options nor the policy carry it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BlankEntry_IsSkippedAndHostStartsAsync()
    {
        await using var host = WithOrigins(TestOrigin, string.Empty, "  ");

        var options = host.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = await host.Services.GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(new DefaultHttpContext(), policyName: null);

        options.Origins.Should().BeEquivalentTo([TestOrigin]);
        policy.Should().NotBeNull();
        policy!.Origins.Should().BeEquivalentTo([TestOrigin]);
    }

    /// <summary>An entry that is not an absolute URI refuses startup, naming the setting.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task RelativeEntry_RefusesStartupAsync()
    {
        await using var host = WithOrigins(TestOrigin, "client.test");

        var act = () => host.Services;

        act.Should().Throw<Exception>()
            .Where(e => e.ToString().Contains("Cors:AllowedOrigins", StringComparison.Ordinal));
    }

    /// <summary>Derives a host whose <c>Cors:AllowedOrigins</c> are exactly <paramref name="origins"/>.</summary>
    /// <param name="origins">The entries, in index order.</param>
    /// <returns>The derived host.</returns>
    private WebApplicationFactory<Program> WithOrigins(params string[] origins) =>
        _factory.WithWebHostBuilder(builder =>
        {
            for (var i = 0; i < origins.Length; i++)
            {
                builder.UseSetting($"Cors:AllowedOrigins:{i}", origins[i]);
            }
        });
}
