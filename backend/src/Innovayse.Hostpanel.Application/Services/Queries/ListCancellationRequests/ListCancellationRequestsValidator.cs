using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListCancellationRequests;

/// <summary>
/// Validates <see cref="ListCancellationRequestsQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the type and status filters.
/// </summary>
public sealed class ListCancellationRequestsValidator : ListQueryValidator<ListCancellationRequestsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCancellationRequestsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["CancellationTypeInvalid"]);
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["CancellationStatusInvalid"]);
    }
}
