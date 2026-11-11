using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyBankTransfer;

/// <summary>
/// Validator for <see cref="GetMyBankTransferQuery"/>. Checks only the shape of the id; whether it names an invoice the
/// caller may read is the handler's answer.
/// </summary>
public sealed class GetMyBankTransferValidator : LocalizedValidator<GetMyBankTransferQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyBankTransferValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
    }
}
