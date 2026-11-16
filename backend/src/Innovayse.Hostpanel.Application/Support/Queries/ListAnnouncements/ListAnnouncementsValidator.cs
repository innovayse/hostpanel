using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListAnnouncements;

/// <summary>
/// Validates <see cref="ListAnnouncementsQuery"/>: the page, page size and search every list shares
/// (<see cref="ListQueryValidator{T}"/>). The published state is a plain <c>bool?</c>, which the model binder refuses when it is not one.
/// </summary>
public sealed class ListAnnouncementsValidator : ListQueryValidator<ListAnnouncementsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListAnnouncementsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
