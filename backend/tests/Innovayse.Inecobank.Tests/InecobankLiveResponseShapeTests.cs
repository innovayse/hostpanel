namespace Innovayse.Inecobank.Tests;

using System.Net;
using FluentAssertions;
using Innovayse.Providers.Inecobank;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Tests for the response shapes the live Inecobank gateway actually returns, as opposed to the
/// ones its merchant manual documents.
/// </summary>
/// <remarks>
/// Every case here was captured from the production gateway with real merchant credentials, and
/// each one used to be mishandled. The manual describes a 200 response carrying
/// <c>errorCode</c>/<c>errorMessage</c>; the gateway also answers failures with a non-2xx status
/// and a body keyed <c>code</c>/<c>message</c>. Reading only the documented shape made a business
/// answer look like a transport failure, and — worse — made a failure body parse as a success
/// with code 0.
/// </remarks>
public class InecobankLiveResponseShapeTests
{
    /// <summary>Body the live gateway returns for an order id it has no record of.</summary>
    private const string WrongOrderIdBody =
        """{"message":"Wrong order id","exceptionType":"OrderStatusException","code":"9500"}""";

    /// <summary>Body the live gateway returns when the merchant credentials are refused.</summary>
    private const string UnauthorizedBody = """{"message":"Unauthorized"}""";

    /// <summary>Builds a gateway over a fake transport.</summary>
    /// <returns>The gateway and the handler queuing its responses.</returns>
    private static (InecobankPaymentGateway Gateway, FakeHttpMessageHandler Http) CreateGateway()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["integration:inecobank:gateway_url"] = "https://testpg.example.am",
                ["integration:inecobank:username"] = "merchant-api",
                ["integration:inecobank:password"] = "secret-pw",
            })
            .Build();

        var handler = new FakeHttpMessageHandler();
        return (
            new InecobankPaymentGateway(config, NullLogger<InecobankPaymentGateway>.Instance, new HttpClient(handler)),
            handler);
    }

    /// <summary>
    /// An unknown order comes back as Declined rather than an exception, even though the gateway
    /// reports it with HTTP 500 and the undocumented code 9500.
    /// </summary>
    /// <remarks>
    /// Two things depend on this. The reconciliation job asks about sessions the bank may never
    /// have registered and has to settle them rather than raise; and the admin panel's connection
    /// probe is this exact call with a bogus id, so a throw here reported "test failed" for
    /// credentials the bank had just accepted.
    /// </remarks>
    [Fact]
    public async Task GetStatusAsync_WhenTheGatewayReports500WrongOrderId_ReturnsDeclined()
    {
        var (gateway, http) = CreateGateway();
        http.EnqueueStatus(HttpStatusCode.InternalServerError, WrongOrderIdBody);

        var status = await gateway.GetStatusAsync("connection-test-probe", CancellationToken.None);

        status.State.Should().Be(GatewayPaymentState.Declined);
    }

    /// <summary>
    /// Refused credentials surface as a distinct failure, not as a generic transport error.
    /// </summary>
    /// <remarks>
    /// An operator has to be able to tell "the password is wrong" from "the bank is unreachable":
    /// one is a setting to correct, the other is something to retry.
    /// </remarks>
    [Fact]
    public async Task GetStatusAsync_WhenTheGatewayReturns401_ThrowsSayingCredentialsWereRejected()
    {
        var (gateway, http) = CreateGateway();
        http.EnqueueStatus(HttpStatusCode.Unauthorized, UnauthorizedBody);

        var act = async () => await gateway.GetStatusAsync("gw-1", CancellationToken.None);

        (await act.Should().ThrowAsync<InecobankApiException>())
            .Which.Message.Should().Contain("rejected the merchant credentials");
    }

    /// <summary>
    /// A failure body keyed <c>code</c>/<c>message</c> is read as a failure.
    /// </summary>
    /// <remarks>
    /// This is the quiet one. Reading only the documented <c>errorCode</c> left an error body
    /// looking like <c>errorCode: 0</c> — a success — so a registration that the bank had
    /// refused would have been handed to the payer as a payment session.
    /// </remarks>
    [Fact]
    public async Task CreatePaymentAsync_WhenTheErrorUsesTheCodeField_IsTreatedAsAFailure()
    {
        var (gateway, http) = CreateGateway();
        http.EnqueueStatus(
            HttpStatusCode.InternalServerError,
            """{"message":"Merchant is blocked","code":"9001"}""");

        var act = async () => await gateway.CreatePaymentAsync(
            new PaymentRequest("INV1-1755850000", 1000, "https://portal/result?invoice=1", "Invoice #1", null),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InecobankApiException>())
            .Which.Message.Should().Contain("Merchant is blocked");
    }

    /// <summary>
    /// When the body explains nothing, the HTTP status is reported instead — it is then the only
    /// thing that tells an operator what happened.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_WhenTheBodyIsNotJson_ReportsTheHttpStatus()
    {
        var (gateway, http) = CreateGateway();
        http.EnqueueStatus(HttpStatusCode.BadGateway, "<html><body>502 Bad Gateway</body></html>");

        var act = async () => await gateway.GetStatusAsync("gw-1", CancellationToken.None);

        (await act.Should().ThrowAsync<InecobankApiException>())
            .Which.Message.Should().Contain("502");
    }
}
