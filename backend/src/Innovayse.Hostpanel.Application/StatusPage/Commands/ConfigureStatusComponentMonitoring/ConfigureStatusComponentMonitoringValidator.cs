using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ConfigureStatusComponentMonitoring;

/// <summary>Validates <see cref="ConfigureStatusComponentMonitoringCommand"/> before it reaches the handler.</summary>
public sealed class ConfigureStatusComponentMonitoringValidator : LocalizedValidator<ConfigureStatusComponentMonitoringCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ConfigureStatusComponentMonitoringValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.CheckUrl).NotEmpty().WithMessage(L["StatusCheckUrlRequired"]).When(x => x.Enabled);

        // An address is checked whenever one is given, switched on or not, so a stored address is never a bad one.
        RuleFor(x => x.CheckUrl)
            .MaximumLength(StatusComponent.CheckUrlMaxLength).WithMessage(L["StatusCheckUrlTooLong", StatusComponent.CheckUrlMaxLength])
            .MustBeAbsoluteWebUrl(L["StatusCheckUrlInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.CheckUrl));
        RuleFor(x => x.FailureThreshold).InclusiveBetween(1, StatusComponent.MaxFailureThreshold)
            .WithMessage(L["StatusFailureThresholdInvalid", StatusComponent.MaxFailureThreshold]);
    }
}
