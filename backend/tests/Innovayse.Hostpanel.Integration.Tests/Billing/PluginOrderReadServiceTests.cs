using Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="PluginOrderReadService"/> against Postgres: the checks a payment callback's request to read an order passes
/// before the use case runs (the shape of the id, the plugin's budget for that order, which fails closed), that no failure
/// of the read ever reaches the plugin, and that only the caller's own cancellation does. The plugin limiter is switched on
/// with a budget of one read a minute per order, and the payment plugin is a fake.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginOrderReadServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The plugin id the seeded sessions belong to.</summary>
    private const string Gateway = "bank-gateway";

    /// <summary>The longest gateway order id the host takes.</summary>
    private const int MaxOrderIdLength = 255;

    /// <summary>A pending order is read once; a second request for the same order inside the window is ignored without a call to the gateway.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SecondRequestForTheSameOrder_WithinTheBudgetWindow_IsIgnored()
    {
        var plugin = new FakePaymentPlugin();
        var host = BuildHost(plugin);
        var (_, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var first = await RequestAsync(host, orderId, CancellationToken.None);
        var second = await RequestAsync(host, orderId, CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(0);
        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>The budget is per order: another order of the same plugin is read in the same window.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARequestForAnotherOrder_HasItsOwnBudget()
    {
        var plugin = new FakePaymentPlugin();
        var host = BuildHost(plugin);
        var (_, firstOrder) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        var (_, secondOrder) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        await RequestAsync(host, firstOrder, CancellationToken.None);
        var other = await RequestAsync(host, secondOrder, CancellationToken.None);

        other.Should().Be(1);
        plugin.GetStatusCallCount.Should().Be(2);
    }

    /// <summary>An id that is blank, too long, padded or holds a control character is ignored before the budget or the use case is touched.</summary>
    /// <param name="orderId">The id the plugin sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded ")]
    [InlineData("line\nbreak")]
    [InlineData("nul\0id")]
    public async Task AnIdOfTheWrongShape_IsIgnored(string orderId)
    {
        var host = BuildHost(new FakePaymentPlugin());

        (await RequestAsync(host, orderId, CancellationToken.None)).Should().Be(0);
    }

    /// <summary>An id over the stored length is ignored without a read.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnIdOverTheStoredLength_IsIgnored()
    {
        var host = BuildHost(new FakePaymentPlugin());

        (await RequestAsync(host, new string('x', MaxOrderIdLength + 1), CancellationToken.None)).Should().Be(0);
    }

    /// <summary>A plugin whose status read throws never learns of it: the request returns normally and nothing is settled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AFailingRead_IsSwallowed()
    {
        var plugin = new FakePaymentPlugin { StatusThrows = new InvalidOperationException("gateway down") };
        var host = BuildHost(plugin);
        var (_, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var act = () => RequestAsync(host, orderId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        plugin.GetStatusCallCount.Should().Be(1);
    }

    /// <summary>A caller that gave up is the one failure that is not swallowed: its own cancellation propagates.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheCallersOwnCancellation_Propagates()
    {
        var host = BuildHost(new FakePaymentPlugin());
        var (_, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PluginOrderReadService>();

        var act = () => service.RequestAsync(PluginCallBinding.WithoutClient(Gateway), orderId, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>Builds a host whose payment plugin is <paramref name="plugin"/> and whose order read budget is one a minute per order.</summary>
    /// <param name="plugin">The fake plugin the bank gateway resolves to.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPlugin plugin)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Gateway, plugin);
        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IPaymentPluginResolver>(resolver);
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(new PluginRateLimiter(
                new RateLimitOptions { PluginOrderRead = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 } },
                disabled: false));
        }));
    }

    /// <summary>
    /// Asks the service to read an order as a callback of <see cref="Gateway"/> would, and counts how many times the use case
    /// ran meanwhile.
    /// </summary>
    /// <param name="host">The host whose service is asked.</param>
    /// <param name="gatewayOrderId">The order id the plugin sent.</param>
    /// <param name="ct">The caller's cancellation token.</param>
    /// <returns>How many times <see cref="ReadGatewayOrderCommand"/> was handled.</returns>
    private static async Task<int> RequestAsync(WebApplicationFactory<Program> host, string gatewayOrderId, CancellationToken ct)
    {
        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var scope = host.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PluginOrderReadService>()
                    .RequestAsync(PluginCallBinding.WithoutClient(Gateway), gatewayOrderId, ct);
            }));

        return session.Executed.MessagesOf<ReadGatewayOrderCommand>().Count();
    }
}
