using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Locations.Common;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Locations.Commands.CreateLocation;

/// <summary>Validates <see cref="CreateLocationCommand"/> before it reaches the handler.</summary>
public sealed class CreateLocationValidator : LocalizedValidator<CreateLocationCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateLocationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CountryCode).NotEmpty().WithMessage(L["LocationCountryCodeInvalid"]).Matches("^[A-Za-z]{2}$").WithMessage(L["LocationCountryCodeInvalid"]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage(L["ContentSortOrderInvalid"]);
        RuleFor(x => x.Translations).NotEmpty().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Translations).MustIncludeDefaultLocale(L["ContentDefaultTranslationRequired"]);
        RuleForEach(x => x.Translations).SetValidator(new LocationTranslationValidator(localizer));
    }
}
