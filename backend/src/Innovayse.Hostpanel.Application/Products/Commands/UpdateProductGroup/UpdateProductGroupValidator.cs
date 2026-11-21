using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductGroup;

/// <summary>Validates <see cref="UpdateProductGroupCommand"/> before it reaches the handler.</summary>
public sealed class UpdateProductGroupValidator : LocalizedValidator<UpdateProductGroupCommand>
{
    /// <summary>Longest a group name may be.</summary>
    private const int NameMaxLength = 100;

    /// <summary>Longest a description may be.</summary>
    private const int DescriptionMaxLength = 500;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateProductGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ProductGroupIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ProductGroupNameRequired"])
            .MaximumLength(NameMaxLength).WithMessage(L["ProductGroupNameTooLong", NameMaxLength]);
        RuleFor(x => x.Description).MaximumLength(DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage(L["ProductGroupDescriptionTooLong", DescriptionMaxLength]);
        RuleFor(x => x.AllowedPaymentMethods).MustBePaymentMethodList(
            L["ProductGroupPaymentMethodsTooMany", ValidationRuleExtensions.PaymentMethodsMaxCount],
            L["ProductGroupPaymentMethodTooLong", ValidationRuleExtensions.PaymentMethodMaxLength]);
    }
}
