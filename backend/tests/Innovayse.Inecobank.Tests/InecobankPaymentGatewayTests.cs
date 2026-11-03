using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Providers.Inecobank;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Inecobank.Tests;

/// <summary>Tests for <see cref="InecobankPaymentGateway"/> config handling and status mapping.</summary>
public class InecobankPaymentGatewayTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the gateway; the client's requests are redirected to it.</summary>
    private StubHttpServer _http = null!;

    /// <summary>Starts the stub gateway before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        _http = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub gateway after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await _http.DisposeAsync();
    }

    /// <summary>Builds the HTTP client every request of which reaches the stub gateway.</summary>
    /// <returns>The client.</returns>
    private HttpClient CreateHttpClient() => new(new RedirectToStubHandler(_http.BaseAddress));

    /// <summary>Builds a configuration holding valid gateway settings.</summary>
    /// <param name="overrides">Settings that replace or add to the defaults.</param>
    /// <returns>The configuration.</returns>
    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["integration:inecobank:gateway_url"] = "https://testpg.example.am",
            ["integration:inecobank:username"] = "merchant-api",
            ["integration:inecobank:password"] = "secret-pw",
        };
        foreach (var (k, v) in overrides ?? [])
        {
            values[k] = v;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Builds a gateway wired to the stub gateway.</summary>
    /// <param name="configOverrides">Settings that replace or add to the defaults.</param>
    /// <returns>The gateway under test.</returns>
    private InecobankPaymentGateway CreateGateway(
        Dictionary<string, string?>? configOverrides = null)
    {
        return new InecobankPaymentGateway(
            BuildConfig(configOverrides),
            NullLogger<InecobankPaymentGateway>.Instance,
            CreateHttpClient());
    }

    /// <summary>Create payment async, uses configured currency defaults.</summary>
    [Fact]
    public async Task CreatePaymentAsync_UsesConfiguredCurrencyDefaults()
    {
        var gateway = CreateGateway();
        _http.Enqueue("""{"orderId":"gw-1","formUrl":"https://testpg.example.am/pay?mdOrder=gw-1"}""");

        var session = await gateway.CreatePaymentAsync(
            new PaymentInstruction("INV7-1755850000", 120000, "https://portal/result?invoice=7", "Invoice #7", null),
            CancellationToken.None);

        session.Should().Be(new PaymentSession("gw-1", "https://testpg.example.am/pay?mdOrder=gw-1"));
        _http.Requests[0].Body.Should().Contain("currency=051").And.Contain("language=hy");
    }

    /// <summary>Currency code, defaults to051 amd.</summary>
    [Fact]
    public void CurrencyCode_DefaultsTo051Amd()
    {
        var gateway = CreateGateway();

        gateway.CurrencyCode.Should().Be("051");
    }

    /// <summary>Currency code, uses configured value.</summary>
    [Fact]
    public void CurrencyCode_UsesConfiguredValue()
    {
        var gateway = CreateGateway(new() { ["integration:inecobank:currency"] = "840" });

        gateway.CurrencyCode.Should().Be("840");
    }

    /// <summary>Create payment async, missing required config, throws.</summary>
    [Fact]
    public async Task CreatePaymentAsync_MissingRequiredConfig_Throws()
    {
        var gateway = CreateGateway(new() { ["integration:inecobank:username"] = null });

        var act = () => gateway.CreatePaymentAsync(
            new PaymentInstruction("INV1-1", 100, "https://portal/r", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Get status async, maps order status.</summary>
    /// <param name="orderStatus">The gateway order status number.</param>
    /// <param name="expected">The payment state it should map to.</param>
    [Theory]
    [InlineData(2, GatewayPaymentState.Paid)]
    [InlineData(0, GatewayPaymentState.Pending)]
    [InlineData(1, GatewayPaymentState.Pending)]
    [InlineData(5, GatewayPaymentState.Pending)]
    [InlineData(3, GatewayPaymentState.Declined)]
    [InlineData(4, GatewayPaymentState.Declined)]
    [InlineData(6, GatewayPaymentState.Declined)]
    public async Task GetStatusAsync_MapsOrderStatus(int orderStatus, GatewayPaymentState expected)
    {
        var gateway = CreateGateway();
        _http.Enqueue($$"""{"errorCode":0,"orderStatus":{{orderStatus}},"authRefNum":"ref-1"}""");

        var status = await gateway.GetStatusAsync("gw-1", CancellationToken.None);

        status.State.Should().Be(expected);
        if (expected == GatewayPaymentState.Paid)
        {
            status.TransactionId.Should().Be("ref-1");
        }
    }

    /// <summary>Get status async, unknown order, error code6, is declined, not thrown.</summary>
    [Fact]
    public async Task GetStatusAsync_UnknownOrder_ErrorCode6_IsDeclined_NotThrown()
    {
        var gateway = CreateGateway();
        _http.Enqueue("""{"errorCode":6,"errorMessage":"Unregistered OrderId"}""");

        var status = await gateway.GetStatusAsync("bogus", CancellationToken.None);

        status.State.Should().Be(GatewayPaymentState.Declined);
    }

    /// <summary>Get status async, access denied, error code5, throws.</summary>
    [Fact]
    public async Task GetStatusAsync_AccessDenied_ErrorCode5_Throws()
    {
        var gateway = CreateGateway();
        _http.Enqueue("""{"errorCode":5,"errorMessage":"Access denied"}""");

        var act = () => gateway.GetStatusAsync("gw-1", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<InecobankApiException>()).Which;
        ex.ErrorCode.Should().Be(5);
        ex.Message.Should().NotContain("Access denied");
    }

    /// <summary>Get status async, paid without auth ref num, falls back to gateway order id.</summary>
    [Fact]
    public async Task GetStatusAsync_PaidWithoutAuthRefNum_FallsBackToGatewayOrderId()
    {
        var gateway = CreateGateway();
        _http.Enqueue("""{"errorCode":0,"orderStatus":2}""");

        var status = await gateway.GetStatusAsync("gw-9", CancellationToken.None);

        status.TransactionId.Should().Be("gw-9");
    }

    /// <summary>Refund async, returns gateway order id as reference.</summary>
    [Fact]
    public async Task RefundAsync_ReturnsGatewayOrderIdAsReference()
    {
        var gateway = CreateGateway();
        _http.Enqueue("""{"errorCode":0}""");

        var reference = await gateway.RefundAsync("gw-3", 5000, CancellationToken.None);

        reference.Should().Be("gw-3");
        _http.Requests[0].PathAndQuery.Should().EndWith("/payment/rest/refund.do");
    }
}
