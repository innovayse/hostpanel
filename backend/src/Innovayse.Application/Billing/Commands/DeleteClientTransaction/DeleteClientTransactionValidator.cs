namespace Innovayse.Application.Billing.Commands.DeleteClientTransaction;

using FluentValidation;

/// <summary>Validates <see cref="DeleteClientTransactionCommand"/>.</summary>
public sealed class DeleteClientTransactionValidator : AbstractValidator<DeleteClientTransactionCommand>
{
    /// <summary>Initialises all validation rules.</summary>
    public DeleteClientTransactionValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
