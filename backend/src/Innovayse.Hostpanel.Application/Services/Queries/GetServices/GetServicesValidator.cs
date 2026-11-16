using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServices;

/// <summary>
/// Validates <see cref="GetServicesQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the service filters.
/// </summary>
public sealed class GetServicesValidator : ListQueryValidator<GetServicesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetServicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ServiceClientIdFilterInvalid"]);
        RuleFor(x => x.ProductId).MustBePositiveId(L["ProductIdInvalid"]);

        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["ServiceListStatusInvalid"]);
        RuleFor(x => x.BillingCycle).IsInEnum().WithMessage(L["ServiceListBillingCycleInvalid"]);
    }
}
