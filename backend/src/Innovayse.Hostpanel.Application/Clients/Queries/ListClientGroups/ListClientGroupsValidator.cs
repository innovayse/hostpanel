using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Queries.ListClientGroups;

/// <summary>Validates <see cref="ListClientGroupsQuery"/>; the query carries no input, so there is no rule.</summary>
public sealed class ListClientGroupsValidator : LocalizedValidator<ListClientGroupsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientGroupsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
