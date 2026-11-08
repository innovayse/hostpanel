using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;

/// <summary>Validates <see cref="GetOwnedDomainNamesQuery"/> before it reaches the handler.</summary>
public sealed class GetOwnedDomainNamesValidator : LocalizedValidator<GetOwnedDomainNamesQuery>
{
    /// <summary>Initializes the validator with the localized messages.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetOwnedDomainNamesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["DomainClientIdRequired"]);
        RuleForEach(x => x.DomainNames).NotEmpty().WithMessage(L["DomainNameRequired"]);
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(L["OrderIdInvalid"]).When(x => x.OrderId is not null);
    }
}
