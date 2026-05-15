namespace Innovayse.Application.Clients.Commands.AdminCreateClient;

using FluentValidation;

/// <summary>
/// Validates <see cref="AdminCreateClientCommand"/> before the handler executes.
/// Applies conditional rules based on whether a new user is being created or an existing one is linked.
/// </summary>
public sealed class AdminCreateClientValidator : AbstractValidator<AdminCreateClientCommand>
{
    /// <summary>Initialises validation rules for admin client creation.</summary>
    public AdminCreateClientValidator()
    {
        // ── New user creation rules ────────────────────────────────────────
        When(x => x.CreateNewUser, () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required when creating a new user.")
                .EmailAddress().WithMessage("Email must be a valid email address.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required when creating a new user.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        });

        // ── Existing user linking rules ────────────────────────────────────
        When(x => !x.CreateNewUser, () =>
        {
            RuleFor(x => x.ExistingUserId)
                .NotEmpty().WithMessage("ExistingUserId is required when linking an existing user.");
        });

        // ── Always-required fields ─────────────────────────────────────────
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        // ── Optional field length limits ───────────────────────────────────
        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.")
            .When(x => x.CompanyName is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Country)
            .Length(2).WithMessage("Country must be a 2-character ISO 3166-1 alpha-2 code.")
            .When(x => x.Country is not null);

        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be a 3-character ISO 4217 code.")
            .When(x => x.Currency is not null);

        RuleFor(x => x.AdminNotes)
            .MaximumLength(2000).WithMessage("Admin notes must not exceed 2000 characters.")
            .When(x => x.AdminNotes is not null);

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).WithMessage("Payment method must not exceed 50 characters.")
            .When(x => x.PaymentMethod is not null);

        RuleFor(x => x.BillingContact)
            .MaximumLength(256).WithMessage("Billing contact must not exceed 256 characters.")
            .When(x => x.BillingContact is not null);
    }
}
