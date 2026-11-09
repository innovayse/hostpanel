using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Locations.Common;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Locations.Commands.UpdateLocation;

/// <summary>Validates <see cref="UpdateLocationCommand"/> before it reaches the handler.</summary>
public sealed class UpdateLocationValidator : LocalizedValidator<UpdateLocationCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateLocationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.CountryCode).NotEmpty().WithMessage(L["LocationCountryCodeInvalid"]).Matches("^[A-Za-z]{2}$").WithMessage(L["LocationCountryCodeInvalid"]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage(L["ContentSortOrderInvalid"]);
        RuleFor(x => x.Translations).NotEmpty().WithMessage(L["ContentTranslationsRequired"]);
        RuleForEach(x => x.Translations).SetValidator(new LocationTranslationValidator(localizer));
    }
}
