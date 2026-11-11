using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetTldConfig;

/// <summary>Validates <see cref="GetTldConfigQuery"/> inputs before the handler executes.</summary>
public sealed class GetTldConfigValidator : LocalizedValidator<GetTldConfigQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetTldConfigValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["TldConfigIdRequired"]);
    }
}
