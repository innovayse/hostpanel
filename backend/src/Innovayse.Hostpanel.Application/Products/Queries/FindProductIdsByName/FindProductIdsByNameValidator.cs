using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.FindProductIdsByName;

/// <summary>Validates <see cref="FindProductIdsByNameQuery"/> before it reaches the handler.</summary>
public sealed class FindProductIdsByNameValidator : LocalizedValidator<FindProductIdsByNameQuery>
{
    /// <summary>Initializes the validator with the localized messages.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FindProductIdsByNameValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Search).MaximumLength(PagingLimits.MaxSearchLength).WithMessage(L["ListSearchTooLong", PagingLimits.MaxSearchLength]);
    }
}
