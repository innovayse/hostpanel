namespace Innovayse.Application.Domains.Commands.UpdateDomain;

using FluentValidation;
using Innovayse.Domain.Domains;

/// <summary>Validates <see cref="UpdateDomainCommand"/> before the handler executes.</summary>
/// <remarks>
/// Added because the handler read <c>Status</c> with <c>Enum.Parse</c> and nothing checked it
/// first: a mistyped status came back as an <see cref="ArgumentException"/>, which
/// <c>ExceptionMiddleware</c> has no arm for on purpose, so an admin's typo was answered 500.
/// </remarks>
public sealed class UpdateDomainValidator : AbstractValidator<UpdateDomainCommand>
{
    /// <summary>Initializes validation rules for domain updates.</summary>
    public UpdateDomainValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);

        // Case-sensitive on purpose. The handler parses this with Enum.Parse<DomainStatus> and
        // no ignoreCase flag, so "active" would clear an ignoreCase check here and still throw a
        // line later -- a 500 on a value this validator had just approved. The rule matches the
        // parse it guards rather than a friendlier version of it; make them agree in the handler
        // first if the looser spelling is ever wanted.
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<DomainStatus>(s, out _))
            .WithMessage(
                "Status must be one of: PendingRegistration, PendingTransfer, Active, Expired, "
                + "Redemption, Transferred, Cancelled.");
    }
}
