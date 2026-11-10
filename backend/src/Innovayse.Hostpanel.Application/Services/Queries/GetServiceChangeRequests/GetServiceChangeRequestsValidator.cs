using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceChangeRequests;

/// <summary>Validates <see cref="GetServiceChangeRequestsQuery"/> before the handler executes.</summary>
public sealed class GetServiceChangeRequestsValidator : LocalizedValidator<GetServiceChangeRequestsQuery>
{
    /// <summary>Most rows a page may hold.</summary>
    private const int MaxPageSize = PagingLimits.MaxPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetServiceChangeRequestsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).GreaterThan(0).WithMessage(L["ServiceIdInvalid"]).When(x => x.ServiceId is not null);
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["ServiceChangeStatusInvalid"]).When(x => x.Status is not null);
        RuleFor(x => x.Page).MustBePage(L["ServicePageInvalid"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["ServicePageSizeInvalid"]);
    }
}
