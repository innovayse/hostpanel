using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.AddDnsRecord;

/// <summary>Validates <see cref="AddDnsRecordCommand"/> inputs before the handler executes.</summary>
public sealed class AddDnsRecordValidator : LocalizedValidator<AddDnsRecordCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AddDnsRecordValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainId).GreaterThan(0).WithMessage(L["DomainIdRequired"]);
        RuleFor(x => x.Host).NotEmpty().WithMessage(L["DnsHostRequired"]);
        RuleFor(x => x.Value).NotEmpty().WithMessage(L["DnsValueRequired"]);
        RuleFor(x => x.Ttl).MustBePositiveId(L["DnsTtlPositive"]);

        RuleFor(x => x.Priority)
            .Must(p => p > 0)
            .When(x => x.Type is DnsRecordType.MX or DnsRecordType.SRV)
            .WithMessage(L["DnsPriorityRequiredForMxSrv"]);
    }
}
