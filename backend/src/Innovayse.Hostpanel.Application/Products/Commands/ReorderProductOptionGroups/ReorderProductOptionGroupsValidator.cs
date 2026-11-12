using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.ReorderProductOptionGroups;

/// <summary>Validates <see cref="ReorderProductOptionGroupsCommand"/> before it reaches the handler.</summary>
public sealed class ReorderProductOptionGroupsValidator : LocalizedValidator<ReorderProductOptionGroupsCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReorderProductOptionGroupsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductId).MustBePositiveId(L["ProductIdInvalid"]);
        RuleFor(x => x.GroupIds).NotEmpty().WithMessage(L["ProductOptionGroupIdsRequired"]);
        RuleForEach(x => x.GroupIds).MustBePositiveId(L["ProductOptionGroupIdInvalid"]);
    }
}
