using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;

/// <summary>Validates <see cref="CountProductCommitmentsQuery"/>.</summary>
public sealed class CountProductCommitmentsValidator : LocalizedValidator<CountProductCommitmentsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CountProductCommitmentsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ProductIds).NotNull();
        RuleForEach(x => x.ProductIds).GreaterThan(0).WithMessage(L["ProductIdInvalid"]);
        RuleFor(x => x.ClientId).GreaterThan(0).When(x => x.ClientId is not null).WithMessage(L["ClientIdInvalid"]);
    }
}
