using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Users.Queries.ListUsers;

/// <summary>
///     Validates <see cref="ListUsersQuery"/>. <c>Search</c> is free text and <c>Page</c>/
///     <c>PageSize</c> are clamped by the handler, so every value of every field is accepted:
///     decided, not forgotten.
/// </summary>
public sealed class ListUsersValidator : LocalizedValidator<ListUsersQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListUsersValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
