using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.API.Identity;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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

    /// <summary>A roleless member is answered 503 with the code and Retry-After by the portal policy when the plugin cannot answer.</summary>
    /// <param name="kind">The failure mode.</param>
    [Theory]
    [InlineData("fault")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    [InlineData("conflict")]
    public async Task RolelessMember_GetsServiceUnavailableFromThePolicyWhenThePluginFailsAsync(string kind)
    {
        var (member, _) = await PluginMemberSeeds.SignInRolelessUserThroughAsync(factory, $"fail-read-{kind}", ResolverFor(kind));
        using var signedIn = member;

        var response = await signedIn.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.Contains("Retry-After").Should().BeTrue();
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
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

    /// <summary>A plain refusal stays a 403: a roleless user the working plugin refuses is not told the plugin is down.</summary>
    [Fact]
    public async Task RolelessUserThePluginRefuses_StaysForbiddenNotUnavailableAsync()
    {
        using var member = await PluginMemberSeeds.SignInRolelessUserAsync(factory, "fail-plain-403", new StubClientResolver());

        var response = await member.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The account read degrades instead of failing: 200 with no client while the plugin cannot answer.</summary>
    /// <param name="kind">The failure mode.</param>
    [Theory]
    [InlineData("fault")]
    [InlineData("conflict")]
    public async Task GetMyAccount_Answers200WithNoClientWhenThePluginIsUnavailableAsync(string kind)
    {
        using var member = await PluginMemberSeeds.SignInClientThroughAsync(factory, $"fail-me-{kind}", ResolverFor(kind));

        var response = await member.Client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("clientId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("externalAccount").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("clientPermissions").GetInt32().Should().Be(0);
    }

    /// <summary>Sequential requests with different grants never share an answer: each gets what its own plugin call said.</summary>
    [Fact]
    public async Task SequentialRequestsWithDifferentGrants_NeverShareAnAnswerAsync()
    {
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId);
        var viewer = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "fail-seq-viewer");
        var buyer = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "fail-seq-buyer");
        var plugin = new StubClientResolver();
        var account = new ExternalAccount(PluginMemberSeeds.Provider, accountId, null, null);
        plugin.Grant(viewer.UserId, new ClientAccess(account, CanPurchase: false, CanManagePaymentMethods: false));
        plugin.Grant(buyer.UserId, new ClientAccess(account, CanPurchase: true, CanManagePaymentMethods: false));
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin))));
        using var viewerHttp = host.CreateClient();
        viewerHttp.DefaultRequestHeaders.Authorization = viewer.Client.DefaultRequestHeaders.Authorization;
        using var buyerHttp = host.CreateClient();
        buyerHttp.DefaultRequestHeaders.Authorization = buyer.Client.DefaultRequestHeaders.Authorization;

        var first = await viewerHttp.GetAsync("/api/me/invoices");
        var second = await buyerHttp.GetAsync("/api/me/invoices");
        var third = await viewerHttp.GetAsync("/api/me/invoices");

        first.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        plugin.Calls.Should().Be(3);
    }

    /// <summary>The memo hands an answer back only to the subject it was stored for.</summary>
    [Fact]
    public void Memo_NeverHandsAnAnswerToAnotherSubject()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var memo = new HttpCallerClientMemo(accessor);
        memo.Set("alice", CallerClientAccess.Unavailable);

        memo.TryGet("alice", out var own).Should().BeTrue();
        memo.TryGet("bob", out _).Should().BeFalse();
        own.IdentityUnavailable.Should().BeTrue();
        memo.Forget();
        memo.TryGet("alice", out _).Should().BeFalse();
    }

    /// <summary>With no HttpContext (a background job, or the bus outside a request) the memo keeps nothing and never throws.</summary>
    [Fact]
    public void Memo_WithoutAnHttpContext_CachesNothing()
    {
        var memo = new HttpCallerClientMemo(new HttpContextAccessor());

        memo.Set("alice", CallerClientAccess.Unavailable);

        memo.TryGet("alice", out _).Should().BeFalse();
        memo.Forget();
    }

    /// <summary>Outside a request the resolver still works, asks the plugin on every ask and caches nothing.</summary>
    [Fact]
    public async Task Resolver_WithoutAnHttpContext_WorksAndCachesNothingAsync()
    {
        var subject = $"bg-{Guid.NewGuid():N}";
        var plugin = new StubClientResolver();
        plugin.Grant(subject, new ClientAccess(new ExternalAccount(PluginMemberSeeds.Provider, "acct-bg", null, null), true, false));
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin));
                services.AddScoped<ICurrentUserService>(_ => new FixedUser(subject));
            }));
        using var scope = host.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICallerClientResolver>();

        var first = await resolver.ResolveAsync(CancellationToken.None);
        await resolver.ResolveAsync(CancellationToken.None);

        first.Permissions.Should().HaveFlag(ClientPermission.PlaceNewOrders);
        plugin.Calls.Should().Be(2);
    }

    /// <summary>Every client portal action declares 503 <c>ErrorResponse</c> in the generated API description.</summary>
    [Fact]
    public void EveryClientPortalAction_DeclaresServiceUnavailable()
    {
        var descriptions = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(api => api.ActionDescriptor.EndpointMetadata.Any(
                m => m is RequiresClientPermissionAttribute or AvailableToEveryClientUserAttribute))
            .ToList();

        descriptions.Should().HaveCountGreaterThan(50);
        descriptions
            .Where(api => !api.SupportedResponseTypes.Any(r => r.StatusCode == StatusCodes.Status503ServiceUnavailable))
            .Select(api => $"{api.HttpMethod} {api.RelativePath}")
            .Should().BeEmpty("the convention declares the identity plugin outage on every portal action");
    }
}
