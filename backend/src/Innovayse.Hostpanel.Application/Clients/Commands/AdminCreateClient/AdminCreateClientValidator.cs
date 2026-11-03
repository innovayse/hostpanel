using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;

/// <summary>
/// Validates <see cref="AdminCreateClientCommand"/> before the handler executes. Applies
/// conditional rules based on whether a new user is being created or an existing one is linked.
/// </summary>
public sealed class AdminCreateClientValidator : LocalizedValidator<AdminCreateClientCommand>
{
    /// <summary>Shortest password this panel will provision for a new account.</summary>
    private const int MinimumPasswordLength = 8;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AdminCreateClientValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        When(x => x.CreateNewUser, () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256)
                .WithMessage(L["ClientNewUserEmailInvalid"]);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(MinimumPasswordLength)
                .WithMessage(L["ClientNewUserPasswordInvalid", MinimumPasswordLength]);
        });

        When(x => !x.CreateNewUser, () =>
        {
            RuleFor(x => x.ExistingUserId).NotEmpty().WithMessage(L["ClientUserIdRequired"]);

            // An address sent while linking an existing account only labels the creation event, but
            // it is still stored there and must be one.
            RuleFor(x => x.Email)
                .EmailAddress().MaximumLength(256).WithMessage(L["ClientNewUserEmailInvalid"])
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        });

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

        // Guarded on blank, not on null: the add-client screen sends `country: value || undefined`
        // but other callers send "", and an empty string is "not provided", not a two-character
        // code that happens to be zero characters long.
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

        RuleFor(x => x.Preferences.Currency)
            .MustBeCurrencyCode(L["ClientCurrencyInvalid"])
            .When(x => !string.IsNullOrEmpty(x.Preferences.Currency));

        // Null means Active. A number that names no status is refused here, the same as on update,
        // rather than quietly creating the client Active.
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(L["ClientStatusInvalid"])
            .When(x => x.Status is not null);

        RuleFor(x => x.Preferences.AdminNotes)
            .MaximumLength(2000).WithMessage(L["ClientAdminNotesInvalid"])
            .When(x => x.Preferences.AdminNotes is not null);

        RuleFor(x => x.Preferences.PaymentMethod)
            .MaximumLength(50).WithMessage(L["ClientPaymentMethodInvalid"])
            .When(x => x.Preferences.PaymentMethod is not null);

        RuleFor(x => x.Preferences.BillingContact)
            .MaximumLength(256).WithMessage(L["ClientBillingContactInvalid"])
            .When(x => x.Preferences.BillingContact is not null);
    }
}
