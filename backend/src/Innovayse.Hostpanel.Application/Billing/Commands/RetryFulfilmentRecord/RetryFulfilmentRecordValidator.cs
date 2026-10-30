using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RetryFulfilmentRecord;

/// <summary>Validates <see cref="RetryFulfilmentRecordCommand"/>.</summary>
public sealed class RetryFulfilmentRecordValidator : LocalizedValidator<RetryFulfilmentRecordCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RetryFulfilmentRecordValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.RecordId).MustBePositiveId(L["FulfilmentRecordIdInvalid"]);
    }
}
