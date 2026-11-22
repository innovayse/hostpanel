using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetAllowedPaymentMethods;

/// <summary>Validates <see cref="GetAllowedPaymentMethodsQuery"/>: every id must be a real id.</summary>
public sealed class GetAllowedPaymentMethodsValidator : LocalizedValidator<GetAllowedPaymentMethodsQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetAllowedPaymentMethodsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductIds).NotNull();
        RuleForEach(x => x.ProductIds).MustBePositiveId(L["ProductIdInvalid"]);
    }
}
