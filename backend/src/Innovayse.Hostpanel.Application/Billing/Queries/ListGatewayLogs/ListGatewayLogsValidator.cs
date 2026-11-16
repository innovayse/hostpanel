using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListGatewayLogs;

/// <summary>
/// Validates <see cref="ListGatewayLogsQuery"/>: the page, page size, search and period every list shares
/// (<see cref="PeriodListQueryValidator{T}"/>) and an outcome that is a member of <see cref="Common.Enums.GatewayLogResult"/>.
/// </summary>
public sealed class ListGatewayLogsValidator : PeriodListQueryValidator<ListGatewayLogsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListGatewayLogsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Result).IsInEnum().WithMessage(L["GatewayLogResultInvalid"]);
    }
}
