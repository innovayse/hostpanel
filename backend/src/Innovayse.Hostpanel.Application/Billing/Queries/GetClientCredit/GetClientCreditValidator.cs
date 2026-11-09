using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetClientCredit;

/// <summary>Validates <see cref="GetClientCreditQuery"/>. Page and page size are clamped by the handler.</summary>
public sealed class GetClientCreditValidator : LocalizedValidator<GetClientCreditQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetClientCreditValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["TransactionClientIdInvalid"]);
    }
}
