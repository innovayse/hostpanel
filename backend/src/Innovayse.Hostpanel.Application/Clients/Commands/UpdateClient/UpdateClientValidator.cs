using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;

/// <summary>Validates <see cref="UpdateClientCommand"/> before the handler executes.</summary>
public sealed class UpdateClientValidator : LocalizedValidator<UpdateClientCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateClientValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["ClientIdInvalid"]);
        // Blank means "keep the address the account has". One that is sent must be a well-formed
        // address of the length the account stores.
        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(256).WithMessage(L["ClientNewUserEmailInvalid"])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage(L["ClientFirstNameInvalid"]);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage(L["ClientLastNameInvalid"]);
        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage(L["ClientCompanyNameInvalid"])
            .When(x => x.CompanyName is not null);
        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage(L["ClientPhoneInvalid"])
            .When(x => x.Phone is not null);
        RuleFor(x => x.Address.Street)
            .MaximumLength(200).WithMessage(L["ClientStreetInvalid"])
            .When(x => x.Address.Street is not null);
        RuleFor(x => x.Address.Address2)
            .MaximumLength(200).WithMessage(L["ClientStreetInvalid"])
            .When(x => x.Address.Address2 is not null);
        RuleFor(x => x.Address.City)
            .MaximumLength(100).WithMessage(L["ClientCityInvalid"])
            .When(x => x.Address.City is not null);
        RuleFor(x => x.Address.State)
            .MaximumLength(100).WithMessage(L["ClientStateInvalid"])
            .When(x => x.Address.State is not null);
        RuleFor(x => x.Address.PostCode)
            .MaximumLength(20).WithMessage(L["ClientPostCodeInvalid"])
            .When(x => x.Address.PostCode is not null);
        // Guarded on blank, not on null. An optional field a form left empty arrives as "" from
        // some callers and as null from others, the admin panel itself sends both, depending on
        // the screen, and `is not null` would put the empty string through a Length() rule it
        // cannot satisfy. Blank means "not provided".
        RuleFor(x => x.Address.Country)
            .Length(2).WithMessage(L["ClientCountryInvalid"])
            .When(x => !string.IsNullOrEmpty(x.Address.Country));
        // Checked against the one list that decides what this product answers in, rather than
        // against a literal here: an unlisted code would be stored, read back and then quietly
        // served English, which looks exactly like the save having failed.
        RuleFor(x => x.Preferences.Language)
            .Must(l => LocaleOptions.SupportedLocales.Contains(l!, StringComparer.OrdinalIgnoreCase))
            .WithMessage(L["ClientLanguageInvalid"])
            .When(x => !string.IsNullOrEmpty(x.Preferences.Language));
        // The exact shape Client.SetCurrency requires, so a bad code answers a validation error
        // here instead of throwing there.
        RuleFor(x => x.Preferences.Currency)
            .MustBeCurrencyCode(L["ClientCurrencyInvalid"])
            .When(x => x.Preferences.Currency is not null);
        RuleFor(x => x.Preferences.PaymentMethod)
            .MaximumLength(50).WithMessage(L["ClientPaymentMethodInvalid"])
            .When(x => x.Preferences.PaymentMethod is not null);
        RuleFor(x => x.Preferences.BillingContact)
            .MaximumLength(256).WithMessage(L["ClientBillingContactInvalid"])
            .When(x => x.Preferences.BillingContact is not null);
        RuleFor(x => x.Preferences.AdminNotes)
            .MaximumLength(2000).WithMessage(L["ClientAdminNotesInvalid"])
            .When(x => x.Preferences.AdminNotes is not null);
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(L["ClientStatusInvalid"])
            .When(x => x.Status is not null);
    }
}
