using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Validates the part every quote list query shares: the page, page size and search (<see cref="ListQueryValidator{T}"/>)
/// and the stage and validity filters (<see cref="IQuoteListFilters"/>). A list derives from this and adds the rules of
/// its own members.
/// </summary>
/// <typeparam name="T">The quote list query being validated.</typeparam>
public abstract class QuoteListQueryValidator<T> : ListQueryValidator<T>
    where T : IListQuery, IQuoteListFilters
{
    /// <summary>Initializes the validator and declares the quote filter rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected QuoteListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Stage).IsInEnum().WithMessage(L["QuoteStageInvalid"]);
        RuleFor(x => x.Validity).IsInEnum().WithMessage(L["QuoteValidityInvalid"]);
    }
}
