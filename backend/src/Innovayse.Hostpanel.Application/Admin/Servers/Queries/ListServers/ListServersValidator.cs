using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;

/// <summary>
///     Validator for <see cref="ListServersQuery"/>.
///     Its one input is an optional module filter: every value, including its absence, is a valid
///     filter, so no rule is declared; decided, not forgotten.
/// </summary>
public sealed class ListServersValidator : LocalizedValidator<ListServersQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListServersValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
