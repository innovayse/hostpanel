using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.ClientStatement;

/// <summary>Validates <see cref="ClientStatementQuery"/> inputs before the handler executes.</summary>
public sealed class ClientStatementValidator : LocalizedValidator<ClientStatementQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ClientStatementValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ReportClientIdInvalid"]);
    }
}
