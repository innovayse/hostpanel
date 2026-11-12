using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The rules the host applies to the session a payment plugin registered before it hands the payer to it, and what the
/// portal is told to do with an accepted one: a form that breaks the contract's rules or limits, and a browser step from a
/// plugin that cannot complete a return, are refused.
/// </summary>
public sealed class PaymentSessionExtensionsTests
{
    /// <summary>An action every form test starts from.</summary>
    private const string GoodAction = "https://pg.example/hosted";

    /// <summary>A plugin with no capability beyond the payment contract.</summary>
    private static readonly IPaymentPlugin Plain = new FakePaymentPlugin();

    /// <summary>A plugin that completes a payment from the return request.</summary>
    private static readonly IPaymentPlugin Returning = new FakeReturnPaymentPlugin();

    /// <summary>Builds a form post session.</summary>
    /// <param name="action">The action of the form.</param>
    /// <param name="fields">The fields of the form.</param>
    /// <returns>The session.</returns>
    private static PaymentSession FormSession(string action, params (string Name, string Value)[] fields)
    {
        return new PaymentSession("gw-1", action, new PaymentFormPost(action, [.. fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value))]));
    }

    /// <summary>A redirect is always acted on, whatever the plugin can do, and maps to its address.</summary>
    [Fact]
    public void ARedirect_IsAcceptedAndMapsToItsAddress()
    {
        var session = new PaymentSession("gw-1", "https://pg.example/pay");

        session.IsAcceptedBy(Plain).Should().BeTrue();
        var dto = session.ToRedirectDto();
        dto.Kind.Should().Be(GatewaySessionKind.Redirect);
        dto.RedirectUrl.Should().Be("https://pg.example/pay");
        dto.FormPost.Should().BeNull();
    }

    /// <summary>A browser step needs a plugin that completes a return, and maps to its page.</summary>
    [Fact]
    public void ABrowserStep_IsAcceptedOnlyFromAPluginThatCompletesAReturn()
    {
        var session = new PaymentSession("gw-1", "https://pg.example/step", null, PaymentSessionKind.BrowserStep);

        session.IsAcceptedBy(Returning).Should().BeTrue();
        session.IsAcceptedBy(Plain).Should().BeFalse();
        var dto = session.ToRedirectDto();
        dto.Kind.Should().Be(GatewaySessionKind.BrowserStep);
        dto.RedirectUrl.Should().Be("https://pg.example/step");
        dto.FormPost.Should().BeNull();
    }

    /// <summary>A form post maps to its form, and its address is the form's action.</summary>
    [Fact]
    public void AFormPost_MapsToItsFormAndTakesTheActionAsItsAddress()
    {
        var session = FormSession(GoodAction, ("token", "t1"), ("token", "t2"), ("empty", string.Empty));

        session.IsAcceptedBy(Plain).Should().BeTrue();
        var dto = session.ToRedirectDto();
        dto.Kind.Should().Be(GatewaySessionKind.FormPost);
        dto.RedirectUrl.Should().Be(GoodAction);
        dto.FormPost!.Action.Should().Be(GoodAction);
        dto.FormPost.Fields.Select(f => (f.Name, f.Value)).Should().Equal(("token", "t1"), ("token", "t2"), ("empty", string.Empty));
    }

    /// <summary>A form post whose action is not an absolute https address without user info or control characters is refused.</summary>
    /// <param name="action">The action to test.</param>
    [Theory]
    [InlineData("http://pg.example/hosted")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@pg.example/hosted")]
    [InlineData("https://pg.example/hosted\r\nSet-Cookie: x=1")]
    [InlineData("https://pg.example/hosted\n")]
    public void AFormPost_WithAnUnacceptableAction_IsRefused(string action)
    {
        FormSession(action).IsAcceptedBy(Plain).Should().BeFalse();
    }

    /// <summary>A form post whose action is over the length limit is refused, and one at the limit is accepted.</summary>
    [Fact]
    public void AFormPost_ActionLength_IsHeldToTheLimit()
    {
        var atLimit = GoodAction + "?q=" + new string('a', PaymentFormPostLimits.MaxActionLength - GoodAction.Length - 3);
        atLimit.Length.Should().Be(PaymentFormPostLimits.MaxActionLength);

        FormSession(atLimit).IsAcceptedBy(Plain).Should().BeTrue();
        FormSession(atLimit + "a").IsAcceptedBy(Plain).Should().BeFalse();
    }

    /// <summary>A form post is held to the field count, the name and value lengths, and names that are not empty and have no line break.</summary>
    [Fact]
    public void AFormPost_FieldsAreHeldToTheLimitsAndTheNameRules()
    {
        var atCount = Enumerable.Range(0, PaymentFormPostLimits.MaxFields).Select(i => ($"f{i}", "v")).ToArray();
        FormSession(GoodAction, atCount).IsAcceptedBy(Plain).Should().BeTrue();
        FormSession(GoodAction, [.. atCount, ("one-more", "v")]).IsAcceptedBy(Plain).Should().BeFalse();

        FormSession(GoodAction, (new string('n', PaymentFormPostLimits.MaxNameLength), "v")).IsAcceptedBy(Plain).Should().BeTrue();
        FormSession(GoodAction, (new string('n', PaymentFormPostLimits.MaxNameLength + 1), "v")).IsAcceptedBy(Plain).Should().BeFalse();

        FormSession(GoodAction, ("n", new string('v', PaymentFormPostLimits.MaxValueLength))).IsAcceptedBy(Plain).Should().BeTrue();
        FormSession(GoodAction, ("n", new string('v', PaymentFormPostLimits.MaxValueLength + 1))).IsAcceptedBy(Plain).Should().BeFalse();

        FormSession(GoodAction, (string.Empty, "v")).IsAcceptedBy(Plain).Should().BeFalse();
        FormSession(GoodAction, ("a\nb", "v")).IsAcceptedBy(Plain).Should().BeFalse();
        FormSession(GoodAction, ("a\rb", "v")).IsAcceptedBy(Plain).Should().BeFalse();

        // A value may hold a line break: it is encoded as attribute text, never markup.
        FormSession(GoodAction, ("n", "line1\nline2")).IsAcceptedBy(Plain).Should().BeTrue();
    }
}
