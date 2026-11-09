using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainContact;

/// <summary>Validates <see cref="GetMyDomainContactQuery"/> inputs before the handler executes.</summary>
public sealed class GetMyDomainContactValidator : LocalizedValidator<GetMyDomainContactQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyDomainContactValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
