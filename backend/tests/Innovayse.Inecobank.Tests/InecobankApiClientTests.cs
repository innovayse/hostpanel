namespace Innovayse.Inecobank.Tests;

using FluentAssertions;
using Innovayse.Providers.Inecobank;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>Tests for <see cref="InecobankApiClient"/> request encoding and response parsing.</summary>
public class InecobankApiClientTests
{
    private static readonly InecobankClientOptions Options =
        new("https://testpg.example.am", "merchant-api", "secret-pw");

    private static (InecobankApiClient Client, FakeHttpMessageHandler Http) CreateClient()
    {
        var handler = new FakeHttpMessageHandler();
        var client = new InecobankApiClient(new HttpClient(handler), Options, NullLogger.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task RegisterOrderAsync_SendsCredentialsAmountAndExplicitCurrency()
    {
        var (client, http) = CreateClient();
        http.Enqueue("""{"orderId":"32faa424-858a","formUrl":"https://testpg.example.am/pay?mdOrder=32faa424-858a"}""");

        var result = await client.RegisterOrderAsync(
            new InecobankRegisterRequest("INV42-1755850000", 250000, "051",
                "https://portal.example.com/payment/result?invoice=42", "Invoice #42", "hy"),
            CancellationToken.None);

        result.OrderId.Should().Be("32faa424-858a");
        result.FormUrl.Should().Be("https://testpg.example.am/pay?mdOrder=32faa424-858a");
        http.Requests.Should().ContainSingle();
        http.Requests[0].Url.Should().Be("https://testpg.example.am/payment/rest/register.do");
        var body = http.Requests[0].Body;
        body.Should().Contain("userName=merchant-api");
        body.Should().Contain("password=secret-pw");
        body.Should().Contain("orderNumber=INV42-1755850000");
        body.Should().Contain("amount=250000");
        body.Should().Contain("currency=051"); // gateway defaults to 643 (RUB) when omitted — must always be explicit
        body.Should().Contain("language=hy");
    }

    [Fact]
    public async Task RegisterOrderAsync_SanitizesForbiddenDescriptionCharacters()
    {
        var (client, http) = CreateClient();
        http.Enqueue("""{"orderId":"o1","formUrl":"https://x/pay"}""");

        await client.RegisterOrderAsync(
            new InecobankRegisterRequest("INV1-1", 100, "051", "https://x/r",
                "50% off + extras\r\nline2" + new string('x', 120), null),
            CancellationToken.None);

        var body = System.Net.WebUtility.UrlDecode(http.Requests[0].Body);
        var description = body.Split('&').First(p => p.StartsWith("description="))["description=".Length..];
        description.Should().NotContainAny("%", "+", "\r", "\n");
        description.Length.Should().BeLessThanOrEqualTo(99);
    }

    [Fact]
    public async Task RegisterOrderAsync_NonZeroErrorCode_ThrowsWithCode()
    {
        var (client, http) = CreateClient();
        http.Enqueue("""{"errorCode":"1","errorMessage":"Order already processed"}""");

        var act = () => client.RegisterOrderAsync(
            new InecobankRegisterRequest("INV1-1", 100, "051", "https://x/r", null, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InecobankApiException>();
        ex.Which.ErrorCode.Should().Be(1);
    }

    [Theory]
    [InlineData("""{"errorCode":0,"orderStatus":2,"authRefNum":"ref-777"}""", 0, 2, "ref-777")]
    [InlineData("""{"errorCode":"0","orderStatus":0}""", 0, 0, null)]
    [InlineData("""{"errorCode":6,"errorMessage":"Незарегистрированный OrderId"}""", 6, null, null)]
    public async Task GetOrderStatusAsync_ParsesStringAndNumberErrorCodes(
        string json, int expectedError, int? expectedStatus, string? expectedRef)
    {
        var (client, http) = CreateClient();
        http.Enqueue(json);

        var status = await client.GetOrderStatusAsync("some-order", "hy", CancellationToken.None);

        status.ErrorCode.Should().Be(expectedError);
        status.OrderStatus.Should().Be(expectedStatus);
        status.AuthRefNum.Should().Be(expectedRef);
        http.Requests[0].Url.Should().Be("https://testpg.example.am/payment/rest/getOrderStatusExtended.do");
    }

    [Fact]
    public async Task RefundAsync_SendsOrderIdAndMinorAmount_ThrowsOnError()
    {
        var (client, http) = CreateClient();
        http.Enqueue("""{"errorCode":0}""");
        await client.RefundAsync("order-9", 5000, CancellationToken.None);
        http.Requests[0].Url.Should().Be("https://testpg.example.am/payment/rest/refund.do");
        http.Requests[0].Body.Should().Contain("orderId=order-9").And.Contain("amount=5000");

        http.Enqueue("""{"errorCode":7,"errorMessage":"System error"}""");
        var act = () => client.RefundAsync("order-9", 5000, CancellationToken.None);
        (await act.Should().ThrowAsync<InecobankApiException>()).Which.ErrorCode.Should().Be(7);
    }
}
