using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// An identity plugin that throws, times out or is enabled twice fails closed in one place: the portal policy
/// refuses a roleless member, the permission filter and the order route answer 503
/// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> with <c>Retry-After</c>, no client is created, and a roleless member's
/// request asks a working plugin exactly once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class IdentityPluginFailureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds the port a failure mode stands for.</summary>
    /// <param name="kind">One of <c>fault</c>, <c>timeout</c>, <c>cancelled</c> or <c>conflict</c>.</param>
    /// <returns>The identity plugin port to serve the host with.</returns>
    private static IIdentityPluginResolver ResolverFor(string kind) => kind switch
    {
        "fault" => new FakeIdentityPluginResolver(new FailingClientResolver(new InvalidOperationException("plugin secret token abc"))),
        "timeout" => new FakeIdentityPluginResolver(new FailingClientResolver(new TimeoutException())),
        "cancelled" => new FakeIdentityPluginResolver(new FailingClientResolver(new TaskCanceledException())),
        _ => new ConflictingIdentityPluginResolver(),
    };

    /// <summary>A roleless member is refused a portal read when the plugin cannot answer, and it is not a 500.</summary>
    /// <param name="kind">The failure mode.</param>
    [Theory]
    [InlineData("fault")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    [InlineData("conflict")]
    public async Task RolelessMember_IsRefusedAPortalReadWhenThePluginFailsAsync(string kind)
    {
        var (member, _) = await PluginMemberSeeds.SignInRolelessUserThroughAsync(factory, $"fail-read-{kind}", ResolverFor(kind));
        using var signedIn = member;

        var response = await signedIn.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A client role holder, whom the policy lets through, gets the 503 from the permission filter.</summary>
    /// <param name="kind">The failure mode.</param>
    [Theory]
    [InlineData("fault")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    [InlineData("conflict")]
    public async Task ClientRoleHolder_GetsServiceUnavailableFromThePermissionFilterAsync(string kind)
    {
        using var member = await PluginMemberSeeds.SignInClientThroughAsync(factory, $"fail-filter-{kind}", ResolverFor(kind));

        var response = await member.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.Contains("Retry-After").Should().BeTrue();
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
    }

    /// <summary>The order route answers 503 with the code and creates no client when the plugin cannot answer.</summary>
    /// <param name="kind">The failure mode.</param>
    [Theory]
    [InlineData("fault")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    [InlineData("conflict")]
    public async Task OrderRoute_AnswersUnavailableAndCreatesNoClientWhenThePluginFailsAsync(string kind)
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var (member, userId) = await PluginMemberSeeds.SignInRolelessUserThroughAsync(factory, $"fail-order-{kind}", ResolverFor(kind));
        using var signedIn = member;

        var response = await signedIn.Client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = "Monthly" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.Contains("Retry-After").Should().BeTrue();
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Clients.AsNoTracking().CountAsync(c => c.UserId == userId)).Should().Be(0);
    }

    /// <summary>A roleless member's request asks a working plugin exactly once: policy, filter and use case share the answer.</summary>
    [Fact]
    public async Task RolelessMember_PortalReadAsksThePluginExactlyOnceAsync()
    {
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId);
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "fail-once");
        var plugin = new StubClientResolver();
        plugin.Grant(user.UserId, new ClientAccess(
            new ExternalAccount(PluginMemberSeeds.Provider, accountId, null, null), CanPurchase: true, CanManagePaymentMethods: false));
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = user.Client.DefaultRequestHeaders.Authorization;

        var response = await http.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.Calls.Should().Be(1);
    }
}
