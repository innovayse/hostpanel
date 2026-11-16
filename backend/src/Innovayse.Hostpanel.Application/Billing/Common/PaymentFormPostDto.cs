namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// A hosted payment page that is opened with a form POST. The portal renders it as a form that submits itself; the plugin
/// that described it never supplies markup or script.
/// </summary>
/// <param name="Fields">
/// The fields, in the order they are rendered; a name may repeat. The address the form is posted to is the
/// <see cref="RedirectDto.RedirectUrl"/> of the answer that carries this form, kept in one place only.
/// </param>
public sealed record PaymentFormPostDto(IReadOnlyList<PaymentFormFieldDto> Fields);
