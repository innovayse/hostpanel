using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Commands.DeleteNetworkIssue;

/// <summary>Validator for <see cref="DeleteNetworkIssueCommand"/>.</summary>
public sealed class DeleteNetworkIssueValidator : LocalizedValidator<DeleteNetworkIssueCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteNetworkIssueValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["NetworkIssueIdInvalid"]);
    }
}
