namespace Innovayse.Inecobank.Tests;

using FluentAssertions;
using Innovayse.Providers.Inecobank;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>Tests for <see cref="InecobankPaymentGateway"/> config handling and status mapping.</summary>
public class InecobankPaymentGatewayTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["integration:innovayse-inecobank:gateway_url"] = "https://testpg.example.am",
            ["integration:innovayse-inecobank:username"] = "merchant-api",
            ["integration:innovayse-inecobank:password"] = "secret-pw",
        };
        foreach (var (k, v) in overrides ?? []) values[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static (InecobankPaymentGateway Gateway, FakeHttpMessageHandler Http) CreateGateway(
        Dictionary<string, string?>? configOverrides = null)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = new InecobankPaymentGateway(
            BuildConfig(configOverrides),
            NullLogger<InecobankPaymentGateway>.Instance,
            new HttpClient(handler));
        return (gateway, handler);
    }

    [Fact]
    public async Task CreatePaymentAsync_UsesConfiguredCurrencyDefaults()
    {
        var (gateway, http) = CreateGateway();
        http.Enqueue("""{"orderId":"gw-1","formUrl":"https://testpg.example.am/pay?mdOrder=gw-1"}""");

        var session = await gateway.CreatePaymentAsync(
            new PaymentRequest("INV7-1755850000", 120000, "https://portal/result?invoice=7", "Invoice #7", null),
            CancellationToken.None);

        session.Should().Be(new PaymentSession("gw-1", "https://testpg.example.am/pay?mdOrder=gw-1"));
        http.Requests[0].Body.Should().Contain("currency=051").And.Contain("language=hy");
    }

    [Fact]
    public async Task CreatePaymentAsync_MissingRequiredConfig_Throws()
    {
        var (gateway, _) = CreateGateway(new() { ["integration:innovayse-inecobank:username"] = null });

        var act = () => gateway.CreatePaymentAsync(
            new PaymentRequest("INV1-1", 100, "https://portal/r", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

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
        var (gateway, http) = CreateGateway();
        http.Enqueue($$"""{"errorCode":0,"orderStatus":{{orderStatus}},"authRefNum":"ref-1"}""");

        var status = await gateway.GetStatusAsync("gw-1", CancellationToken.None);

        status.State.Should().Be(expected);
        if (expected == GatewayPaymentState.Paid) status.TransactionId.Should().Be("ref-1");
    }

    [Fact]
    public async Task GetStatusAsync_UnknownOrder_ErrorCode6_IsDeclined_NotThrown()
    {
        var (gateway, http) = CreateGateway();
        http.Enqueue("""{"errorCode":6,"errorMessage":"Unregistered OrderId"}""");

        var status = await gateway.GetStatusAsync("bogus", CancellationToken.None);

        status.State.Should().Be(GatewayPaymentState.Declined);
    }

    [Fact]
    public async Task GetStatusAsync_AccessDenied_ErrorCode5_Throws()
    {
        var (gateway, http) = CreateGateway();
        http.Enqueue("""{"errorCode":5,"errorMessage":"Access denied"}""");

        var act = () => gateway.GetStatusAsync("gw-1", CancellationToken.None);

        (await act.Should().ThrowAsync<InecobankApiException>()).Which.ErrorCode.Should().Be(5);
    }

    [Fact]
    public async Task GetStatusAsync_PaidWithoutAuthRefNum_FallsBackToGatewayOrderId()
    {
        var (gateway, http) = CreateGateway();
        http.Enqueue("""{"errorCode":0,"orderStatus":2}""");

        var status = await gateway.GetStatusAsync("gw-9", CancellationToken.None);

        status.TransactionId.Should().Be("gw-9");
    }

    [Fact]
    public async Task RefundAsync_ReturnsGatewayOrderIdAsReference()
    {
        var (gateway, http) = CreateGateway();
        http.Enqueue("""{"errorCode":0}""");

        var reference = await gateway.RefundAsync("gw-3", 5000, CancellationToken.None);

        reference.Should().Be("gw-3");
        http.Requests[0].Url.Should().EndWith("/payment/rest/refund.do");
    }
}
