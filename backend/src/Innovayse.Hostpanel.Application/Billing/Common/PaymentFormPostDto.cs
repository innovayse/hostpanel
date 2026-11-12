namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// A hosted payment page that is opened with a form POST. The portal renders it as a form that submits itself; the plugin
/// that described it never supplies markup or script.
/// </summary>
/// <param name="Action">The absolute <c>https</c> address the form is posted to.</param>
/// <param name="Fields">The fields, in the order they are rendered; a name may repeat.</param>
public sealed record PaymentFormPostDto(string Action, IReadOnlyList<PaymentFormFieldDto> Fields);
