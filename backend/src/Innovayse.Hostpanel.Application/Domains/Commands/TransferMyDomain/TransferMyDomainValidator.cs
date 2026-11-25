using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.TransferMyDomain;

/// <summary>Validates <see cref="TransferMyDomainCommand"/> inputs before the handler executes.</summary>
/// <remarks>
/// The same shape as <c>TransferDomainValidator</c> minus the client id, which this command does
/// not carry and must not. The year bound is stated here as well as in the TLD price table: a
/// period the table has no entry for is refused later anyway, but a caller who sends 0 or 500
/// should be told so before an order number is burned on it.
/// </remarks>
public sealed class TransferMyDomainValidator : LocalizedValidator<TransferMyDomainCommand>
{
    /// <summary>Minimum accepted length of an EPP/authorization code.</summary>
    private const int MinEppCodeLength = 6;

    /// <summary>Initializes validation rules for a client-initiated domain transfer-in.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public TransferMyDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.DomainName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainNameRequired"])
            .MustBeDomainName(L["DomainNameInvalid"]);
        RuleFor(x => x.EppCode)
            .NotEmpty()
            .MinimumLength(MinEppCodeLength)
            .WithMessage(L["DomainEppCodeRequired"]);
        RuleFor(x => x.Years).InclusiveBetween(1, 10).WithMessage(L["DomainYearsRange"]);
        RuleFor(x => x.PaymentMethod).NotEmpty().WithMessage(L["PaymentMethodRequired"]);
    }
}
