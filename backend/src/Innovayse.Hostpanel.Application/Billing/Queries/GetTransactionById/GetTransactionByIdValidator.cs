using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetTransactionById;

/// <summary>Validates <see cref="GetTransactionByIdQuery"/> before it reaches the handler.</summary>
public sealed class GetTransactionByIdValidator : LocalizedValidator<GetTransactionByIdQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetTransactionByIdValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["TransactionIdInvalid"]);
    }
}
