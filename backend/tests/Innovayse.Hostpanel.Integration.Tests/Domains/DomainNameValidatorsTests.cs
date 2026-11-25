using FluentValidation;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferMyDomain;
using Innovayse.Hostpanel.Application.Domains.Queries.CheckDomainAvailability;
using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Pins every validator that takes a domain name to the one rule, <see cref="DomainNameRules"/>: a name is
/// accepted by all of them or refused by all of them, so a name the storefront lets through can be ordered, and a
/// name registration refuses is never reported as merely taken.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, whose container holds the validators.</param>
public sealed class DomainNameValidatorsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A valid transfer authorization code, long enough for every transfer validator.</summary>
    private const string EppCode = "EPP-SECRET-CODE";

    /// <summary>The payment method a client's own transfer order names.</summary>
    private const string PaymentMethod = "bank-gateway";

    /// <summary>
    /// Every validator answers a name the way <see cref="DomainNameRules.IsValidName"/> does, whether the name is
    /// well-formed or not: an internationalised label, a numeric extension and a malformed label included.
    /// </summary>
    /// <param name="name">A name to put to every validator.</param>
    [Theory]
    [InlineData("example.com")]
    [InlineData("shop.example.co.uk")]
    [InlineData("my-shop.com")]
    [InlineData("123.io")]
    [InlineData("xn--bcher-kva.de")]
    [InlineData("münchen.de")]
    [InlineData("example.123")]
    [InlineData("example.c")]
    [InlineData("bad name!.com")]
    [InlineData("-bad-.com")]
    [InlineData("example")]
    [InlineData("exa..mple.com")]
    [InlineData("example.com.")]
    public void EveryValidator_AnswersTheNameAsTheRuleDoes(string name)
    {
        var expected = DomainNameRules.IsValidName(name);

        AcceptedBy(name).Should().OnlyContain(accepted => accepted == expected);
    }

    /// <summary>
    /// A name padded with white space is refused by the commands, which take it as given, while the availability check
    /// trims it the way its handler does and judges the bare name.
    /// </summary>
    [Fact]
    public void PaddedName_IsRefusedByTheCommandsAndJudgedBareByTheCheck()
    {
        const string padded = "  example.com  ";

        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        provider.GetRequiredService<IValidator<CheckDomainAvailabilityQuery>>()
            .Validate(new CheckDomainAvailabilityQuery(padded)).IsValid.Should().BeTrue();
        provider.GetRequiredService<IValidator<RegisterDomainCommand>>()
            .Validate(RegisterCommand(padded)).IsValid.Should().BeFalse();
        provider.GetRequiredService<IValidator<TransferDomainCommand>>()
            .Validate(new TransferDomainCommand(1, padded, EppCode, WhoisPrivacy: false)).IsValid.Should().BeFalse();
        provider.GetRequiredService<IValidator<TransferMyDomainCommand>>()
            .Validate(new TransferMyDomainCommand(padded, EppCode, 1, PaymentMethod)).IsValid.Should().BeFalse();
        provider.GetRequiredService<IValidator<RenewOrderedDomainCommand>>()
            .Validate(new RenewOrderedDomainCommand(1, padded, 1)).IsValid.Should().BeFalse();
    }

    /// <summary>Puts one name to each validator and reports whether each accepted it.</summary>
    /// <param name="name">The name to validate.</param>
    /// <returns>One flag per validator, in a fixed order, <see langword="true"/> when that validator accepted the name.</returns>
    private bool[] AcceptedBy(string name)
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        return
        [
            provider.GetRequiredService<IValidator<CheckDomainAvailabilityQuery>>()
                .Validate(new CheckDomainAvailabilityQuery(name)).IsValid,
            provider.GetRequiredService<IValidator<RegisterDomainCommand>>().Validate(RegisterCommand(name)).IsValid,
            provider.GetRequiredService<IValidator<TransferDomainCommand>>()
                .Validate(new TransferDomainCommand(1, name, EppCode, WhoisPrivacy: false)).IsValid,
            provider.GetRequiredService<IValidator<TransferMyDomainCommand>>()
                .Validate(new TransferMyDomainCommand(name, EppCode, 1, PaymentMethod)).IsValid,
            provider.GetRequiredService<IValidator<RenewOrderedDomainCommand>>()
                .Validate(new RenewOrderedDomainCommand(1, name, 1)).IsValid,
            provider.GetRequiredService<IValidator<PlaceOrderCommand>>().Validate(OrderWithDomainLine(name)).Errors
                .All(error => !error.PropertyName.EndsWith(nameof(PlaceOrderItemDto.Domain), StringComparison.Ordinal)),
        ];
    }

    /// <summary>Builds an order with one domain registration line for a name, the one member under test.</summary>
    /// <param name="name">The name on the line.</param>
    /// <returns>The command.</returns>
    private static PlaceOrderCommand OrderWithDomainLine(string name)
    {
        var line = new PlaceOrderItemDto(1, BillingCycle.Annual, name, null, DomainAction.Register, null, 1);
        return new PlaceOrderCommand("Ada", "Lovelace", "ada@example.com", null, null, PaymentMethod, [line]);
    }

    /// <summary>Builds a registration command for a name, with every other member valid.</summary>
    /// <param name="name">The name to register.</param>
    /// <returns>The command.</returns>
    private static RegisterDomainCommand RegisterCommand(string name)
    {
        return new RegisterDomainCommand(1, name, 1, WhoisPrivacy: false, AutoRenew: false, Nameserver1: null, Nameserver2: null);
    }
}
