using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Queries.FindClientIds;

/// <summary>Validates <see cref="FindClientIdsQuery"/>: the search text is as long as any list's search may be, and no longer.</summary>
public sealed class FindClientIdsValidator : LocalizedValidator<FindClientIdsQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FindClientIdsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Search).MaximumLength(PagingLimits.MaxSearchLength).WithMessage(L["ListSearchTooLong", PagingLimits.MaxSearchLength]);
    }
}
