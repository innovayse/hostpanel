using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.MarkDomainExpired;

/// <summary>Validates <see cref="MarkDomainExpiredCommand"/> inputs before the handler executes.</summary>
public sealed class MarkDomainExpiredValidator : LocalizedValidator<MarkDomainExpiredCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public MarkDomainExpiredValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
