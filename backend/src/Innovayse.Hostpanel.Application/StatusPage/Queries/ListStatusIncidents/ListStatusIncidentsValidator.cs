using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusIncidents;

/// <summary>Validates <see cref="ListStatusIncidentsQuery"/> before it reaches the handler.</summary>
public sealed class ListStatusIncidentsValidator : LocalizedValidator<ListStatusIncidentsQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListStatusIncidentsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Days).InclusiveBetween(1, ListStatusIncidentsQuery.MaxDays).WithMessage(L["StatusDaysInvalid", ListStatusIncidentsQuery.MaxDays]);
    }
}
