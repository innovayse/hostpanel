namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One field of the form a payer's browser posts to a gateway's hosted page.</summary>
/// <param name="Name">The field name; never empty.</param>
/// <param name="Value">The field value.</param>
public sealed record PaymentFormFieldDto(string Name, string Value);
