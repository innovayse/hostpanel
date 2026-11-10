using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Locations;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Locations.Common;

/// <summary>
/// Validates one entry of a location's translation map: the locale it is keyed by and the translation
/// itself; shared by the create and update validators.
/// </summary>
public sealed class LocationTranslationValidator : LocalizedValidator<KeyValuePair<string, LocationTranslationDto>>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public LocationTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Name).NotEmpty().WithMessage(L["LocationNameRequired"])
            .MaximumLength(ServerLocationTranslation.NameMaxLength).WithMessage(L["LocationNameTooLong", ServerLocationTranslation.NameMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.City).MaximumLength(ServerLocationTranslation.CityMaxLength)
            .WithMessage(L["LocationCityTooLong", ServerLocationTranslation.CityMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.LatencyNote).MaximumLength(ServerLocationTranslation.LatencyNoteMaxLength)
            .WithMessage(L["LocationLatencyNoteTooLong", ServerLocationTranslation.LatencyNoteMaxLength])
            .When(x => x.Value is not null);
    }
}
