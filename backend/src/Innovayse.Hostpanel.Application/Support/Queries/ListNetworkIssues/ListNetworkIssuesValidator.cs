using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListNetworkIssues;

/// <summary>
/// Validates <see cref="ListNetworkIssuesQuery"/>: the page, page size and search every list shares
/// (<see cref="ListQueryValidator{T}"/>) and a status, type and priority that are members of their enums.
/// </summary>
public sealed class ListNetworkIssuesValidator : ListQueryValidator<ListNetworkIssuesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListNetworkIssuesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["NetworkIssueStatusInvalid"]);
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["NetworkIssueTypeInvalid"]);
        RuleFor(x => x.Priority).IsInEnum().WithMessage(L["NetworkIssuePriorityInvalid"]);
    }
}
