namespace Innovayse.Application.Domains.Commands.AddDnsRecord;

using FluentValidation;
using Innovayse.Domain.Domains;

/// <summary>Validates <see cref="AddDnsRecordCommand"/> inputs before the handler executes.</summary>
public sealed class AddDnsRecordValidator : AbstractValidator<AddDnsRecordCommand>
{
    /// <summary>Initializes validation rules for adding a DNS record.</summary>
    public AddDnsRecordValidator()
    {
        RuleFor(x => x.DomainId).GreaterThan(0);
        RuleFor(x => x.Host).NotEmpty();
        RuleFor(x => x.Value).NotEmpty();
        RuleFor(x => x.Ttl).GreaterThan(0);

        RuleFor(x => x.Priority)
            .Must(p => p > 0)
            .When(x => x.Type is DnsRecordType.MX or DnsRecordType.SRV)
            .WithMessage("Priority must be greater than 0 for MX and SRV records.");
    }
}
