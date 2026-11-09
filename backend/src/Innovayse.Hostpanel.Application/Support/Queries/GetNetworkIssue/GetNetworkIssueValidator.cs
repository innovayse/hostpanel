using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.GetNetworkIssue;

/// <summary>Validator for <see cref="GetNetworkIssueQuery"/>.</summary>
public sealed class GetNetworkIssueValidator : LocalizedValidator<GetNetworkIssueQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetNetworkIssueValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["NetworkIssueIdInvalid"]);
    }
}
