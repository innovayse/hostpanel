using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one place the host judges and maps the <see cref="PaymentSession"/> a payment plugin registered: whether the host may
/// act on it, and what the portal is told to do with it. A session the host refuses is a failed payment, never a page it
/// half renders.
/// </summary>
public static class PaymentSessionExtensions
{
    /// <summary>
    /// Whether the host may act on a session. A redirect always can. A form post must keep the rules and limits of
    /// <see cref="PaymentFormPost"/>. A browser step needs a plugin that completes a payment from the return request
    /// (<see cref="IPaymentReturnPlugin"/>), because the host would otherwise wait for a return nobody handles. A kind this
    /// host does not know is refused.
    /// </summary>
    /// <param name="session">The session the plugin registered.</param>
    /// <param name="plugin">The plugin that registered it.</param>
    /// <returns><see langword="true"/> when the session may be handed to the payer.</returns>
    public static bool IsAcceptedBy(this PaymentSession session, IPaymentPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plugin);

        return session.Kind switch
        {
            PaymentSessionKind.Redirect => true,
            PaymentSessionKind.FormPost => session.FormPost is { } form && KeepsFormRules(form),
            PaymentSessionKind.BrowserStep => plugin is IPaymentReturnPlugin,
            _ => false,
        };
    }

    /// <summary>
    /// Maps an accepted session to what the portal needs: the kind, the address and, for a form post, the form. The address of a
    /// form post is the form's action, because the plugin's redirect address is ignored for that kind.
    /// </summary>
    /// <param name="session">A session for which <see cref="IsAcceptedBy"/> answered <see langword="true"/>.</param>
    /// <returns>The data transfer object the start use cases answer with.</returns>
    public static RedirectDto ToRedirectDto(this PaymentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Kind == PaymentSessionKind.FormPost && session.FormPost is { } form)
        {
            return new RedirectDto(
                form.Action,
                GatewaySessionKind.FormPost,
                new PaymentFormPostDto(form.Action, form.Fields.Select(f => new PaymentFormFieldDto(f.Key, f.Value)).ToList()));
        }

        var kind = session.Kind == PaymentSessionKind.BrowserStep ? GatewaySessionKind.BrowserStep : GatewaySessionKind.Redirect;
        return new RedirectDto(session.RedirectUrl, kind, null);
    }

    /// <summary>Whether a form post keeps the rules the plugin contract promises the host will apply.</summary>
    /// <param name="form">The form the plugin described.</param>
    /// <returns>
    /// <see langword="true"/> for an absolute <c>https</c> action without user info or control characters within the length
    /// limit, and at most <see cref="PaymentFormPostLimits.MaxFields"/> fields whose names are not empty, hold no line break and
    /// keep to the name and value limits.
    /// </returns>
    private static bool KeepsFormRules(PaymentFormPost form)
    {
        if (form.Action is not { Length: > 0 and <= PaymentFormPostLimits.MaxActionLength } action
            || action.Any(char.IsControl)
            || !Uri.TryCreate(action, UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps
            || address.UserInfo.Length > 0)
        {
            return false;
        }

        return form.Fields is { Count: <= PaymentFormPostLimits.MaxFields } fields
            && fields.All(field => field.Key is { Length: > 0 and <= PaymentFormPostLimits.MaxNameLength } name
                && !name.Any(c => c is '\r' or '\n')
                && field.Value is { Length: <= PaymentFormPostLimits.MaxValueLength });
    }
}
