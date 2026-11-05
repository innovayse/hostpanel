using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListUpcomingDomainRenewals;

/// <summary>Validates <see cref="ListUpcomingDomainRenewalsQuery"/>.</summary>
public sealed class ListUpcomingDomainRenewalsValidator : LocalizedValidator<ListUpcomingDomainRenewalsQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public ListUpcomingDomainRenewalsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Window).IsInEnum().WithMessage(L["RenewalWindowInvalid"]);
        RuleFor(x => x.Page).MustBePage(L["ServicePageInvalid"]);
        RuleFor(x => x.PageSize).MustBePageSize(PagingLimits.MaxPageSize, L["ServicePageSizeInvalid"]);
    }
}
