using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteMyDomainDnsRecord;

/// <summary>Validates <see cref="DeleteMyDomainDnsRecordCommand"/> before it reaches the handler.</summary>
public sealed class DeleteMyDomainDnsRecordValidator : LocalizedValidator<DeleteMyDomainDnsRecordCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteMyDomainDnsRecordValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).MustBePositiveId(L["DomainIdRequired"]);
        RuleFor(x => x.RecordId).MustBePositiveId(L["DnsRecordIdRequired"]);
    }
}
