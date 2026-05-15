namespace Innovayse.Application.Services.Commands.DeleteCancellationRequest;

using FluentValidation;

/// <summary>Validates <see cref="DeleteCancellationRequestCommand"/> inputs.</summary>
public sealed class DeleteCancellationRequestValidator : AbstractValidator<DeleteCancellationRequestCommand>
{
    /// <summary>Initializes validation rules for cancellation request deletion.</summary>
    public DeleteCancellationRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
