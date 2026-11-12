using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponentGroups;

/// <summary>Validates <see cref="ReorderStatusComponentGroupsCommand"/> before it reaches the handler.</summary>
public sealed class ReorderStatusComponentGroupsValidator : LocalizedValidator<ReorderStatusComponentGroupsCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReorderStatusComponentGroupsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GroupIds).NotEmpty().WithMessage(L["StatusIdsRequired"]);
        RuleForEach(x => x.GroupIds).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
