using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;

/// <summary>Validates <see cref="TransferDomainCommand"/> inputs before the handler executes.</summary>
public sealed class TransferDomainValidator : LocalizedValidator<TransferDomainCommand>
{
    /// <summary>Minimum accepted length of an EPP/authorization code.</summary>
    private const int MinEppCodeLength = 6;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public TransferDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["DomainClientIdRequired"]);
        RuleFor(x => x.DomainName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainNameRequired"])
            .MustBeDomainName(L["DomainNameInvalid"]);
        RuleFor(x => x.EppCode)
            .NotEmpty()
            .MinimumLength(MinEppCodeLength)
            .WithMessage(L["DomainEppCodeRequired"]);
        RuleFor(x => x.Years).InclusiveBetween(1, 10).WithMessage(L["DomainYearsRange"]);
    }
}
