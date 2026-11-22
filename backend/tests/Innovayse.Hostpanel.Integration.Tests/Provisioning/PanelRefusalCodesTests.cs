using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// A panel plugin's coded refusal reaches the caller of every provisioning route that calls a panel (change package, change
/// password, sign in link, credentials) with its own code and status, not the route's generic upstream failure; any other failure
/// keeps that generic code.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PanelRefusalCodesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The routes under test, by name, each sent for one service.</summary>
    private static readonly IReadOnlyDictionary<string, Func<HttpClient, int, Task<HttpResponseMessage>>> Routes =
        new Dictionary<string, Func<HttpClient, int, Task<HttpResponseMessage>>>
        {
            ["change-package"] = (http, id) => http.PostAsJsonAsync($"/api/provisioning/{id}/change-package", new { newPackage = "premium" }),
            ["change-password"] = (http, id) => http.PostAsJsonAsync($"/api/provisioning/{id}/change-password", new { newPassword = "N3wPassw0rd!" }),
            ["cpanel-sso"] = (http, id) => http.GetAsync($"/api/provisioning/{id}/cpanel-sso"),
            ["credentials"] = (http, id) => http.GetAsync($"/api/provisioning/{id}/credentials"),
        };

    /// <summary>Every code of the panel family, spelled out so adding a code to <see cref="HostpanelErrorCodes"/> is a decision this test records.</summary>
    public static readonly TheoryData<string> Family = new()
    {
        HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE,
        HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT,
        HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED,
        HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND,
        HostpanelErrorCodes.PANEL_PACKAGE_UNKNOWN,
        HostpanelErrorCodes.PANEL_USERNAME_INVALID,
        HostpanelErrorCodes.PANEL_DOMAIN_INVALID,
        HostpanelErrorCodes.PANEL_PASSWORD_REJECTED,
        HostpanelErrorCodes.PANEL_LIMIT_REACHED,
        HostpanelErrorCodes.PANEL_ACCESS_DENIED,
        HostpanelErrorCodes.PANEL_REFUSED,
    };

    /// <summary>The set the handlers pass through is exactly the family the error code class declares, and no other code is in it.</summary>
    [Fact]
    public void PanelErrorCodes_AreExactlyTheDeclaredFamily()
    {
        var declared = typeof(HostpanelErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.Name.StartsWith(PanelErrorCodes.Prefix, StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        declared.Should().BeEquivalentTo(Family.Cast<object[]>().Select(row => (string)row[0]));
        declared.Should().OnlyContain(code => PanelErrorCodes.Contains(code));
        PanelErrorCodes.Contains(HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED).Should().BeFalse();
        PanelErrorCodes.Contains(HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN).Should().BeFalse();
    }

    /// <summary>Every code of the family reaches the caller as itself, not as the route's generic upstream failure.</summary>
    /// <param name="code">The panel code the provider refuses with.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(Family))]
    public async Task EveryPanelCode_IsAnsweredAsItselfAsync(string code)
    {
        var serviceId = await SeedProvisionedServiceAsync();
        var provider = new FakeProvisioningProvider { FailWith = new DomainRuleException(code, "the panel refused") };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await Routes["change-package"](http, serviceId);

        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(code);
    }

    /// <summary>A panel refusal is answered with its own code and status.</summary>
    /// <param name="route">The route's name.</param>
    /// <param name="code">The panel code the provider refuses with.</param>
    /// <param name="status">The status that code answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("change-package", HostpanelErrorCodes.PANEL_PACKAGE_UNKNOWN, HttpStatusCode.Conflict)]
    [InlineData("change-password", HostpanelErrorCodes.PANEL_PASSWORD_REJECTED, HttpStatusCode.Conflict)]
    [InlineData("cpanel-sso", HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND, HttpStatusCode.Conflict)]
    [InlineData("credentials", HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, HttpStatusCode.Conflict)]
    [InlineData("change-package", HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE, HttpStatusCode.ServiceUnavailable)]
    [InlineData("change-password", HostpanelErrorCodes.PANEL_ACCESS_DENIED, HttpStatusCode.BadGateway)]
    [InlineData("cpanel-sso", HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT, HttpStatusCode.Conflict)]
    [InlineData("credentials", HostpanelErrorCodes.PANEL_REFUSED, HttpStatusCode.BadGateway)]
    public async Task PanelRefusal_IsAnsweredWithItsOwnCodeAsync(string route, string code, HttpStatusCode status)
    {
        var serviceId = await SeedProvisionedServiceAsync();
        var provider = new FakeProvisioningProvider { FailWith = new DomainRuleException(code, "the panel refused") };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await Routes[route](http, serviceId);

        response.StatusCode.Should().Be(status);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(code);
    }

    /// <summary>A failure that is not a panel code keeps the route's generic upstream failure, and none of its text is answered.</summary>
    /// <param name="route">The route's name.</param>
    /// <param name="code">The generic code the route answers.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("change-package", HostpanelErrorCodes.SERVICE_PACKAGE_UPSTREAM_FAILED)]
    [InlineData("change-password", HostpanelErrorCodes.SERVICE_PASSWORD_UPSTREAM_FAILED)]
    [InlineData("cpanel-sso", HostpanelErrorCodes.SERVICE_CPANEL_SSO_UPSTREAM_FAILED)]
    [InlineData("credentials", HostpanelErrorCodes.SERVICE_CREDENTIALS_UPSTREAM_FAILED)]
    public async Task OtherFailure_KeepsTheGenericUpstreamCodeAsync(string route, string code)
    {
        var serviceId = await SeedProvisionedServiceAsync();
        var provider = new FakeProvisioningProvider { FailWith = new InvalidOperationException("plugin internal text") };
        var http = await ProvisioningTestSeeds.AdminClientWithFakeProviderAsync(factory, provider);

        var response = await Routes[route](http, serviceId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ProvisioningTestSeeds.CodeOfAsync(response)).Should().Be(code);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("plugin internal text");
    }

    /// <summary>Seeds a provisioned service the routes can act on.</summary>
    /// <returns>The service id.</returns>
    private async Task<int> SeedProvisionedServiceAsync()
    {
        var setupHttp = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        var serviceId = await ProvisioningTestSeeds.SeedServiceAsync(factory, setupHttp, adminToken);
        (await setupHttp.PostAsync($"/api/provisioning/{serviceId}/provision", null)).EnsureSuccessStatusCode();
        return serviceId;
    }
}
