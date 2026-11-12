using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponents;

/// <summary>Validates <see cref="ReorderStatusComponentsCommand"/> before it reaches the handler.</summary>
public sealed class ReorderStatusComponentsValidator : LocalizedValidator<ReorderStatusComponentsCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReorderStatusComponentsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GroupId).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.ComponentIds).NotEmpty().WithMessage(L["StatusIdsRequired"]);
        RuleForEach(x => x.ComponentIds).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
