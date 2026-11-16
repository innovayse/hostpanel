using FluentValidation.Results;
using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Orders.Commands.CompleteOrderGatewayPayment;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Pins that the three validators of a gateway payment completion hold the return parameters of the payer's return
/// request to the limits a payment plugin is promised, with the one shared rule, measured on the set the plugin
/// receives. Runs without a database.
/// </summary>
public sealed class CompleteGatewayPaymentReturnParametersValidatorTests
{
    /// <summary>Localizer that echoes keys, so no resource file is needed.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>The validators under test, each as a function from a parameter set to its validation result.</summary>
    public static TheoryData<string> Validators => new() { "invoice", "myInvoice", "order" };

    /// <summary>
    /// Validates a parameter set with the validator named by <paramref name="validator"/>, on an otherwise valid
    /// command.
    /// </summary>
    /// <param name="validator">Which of the three validators to use.</param>
    /// <param name="parameters">The return parameters, or <see langword="null"/> for a check that is not a return.</param>
    /// <returns>The validation result.</returns>
    private static ValidationResult Validate(string validator, IReadOnlyList<PaymentReturnParameterDto>? parameters) =>
        validator switch
        {
            "invoice" => new CompleteGatewayPaymentValidator(Localizer)
                .Validate(new CompleteGatewayPaymentCommand(1, parameters)),
            "myInvoice" => new CompleteMyGatewayPaymentValidator(Localizer)
                .Validate(new CompleteMyGatewayPaymentCommand(1, parameters)),
            _ => new CompleteOrderGatewayPaymentValidator(Localizer)
                .Validate(new CompleteOrderGatewayPaymentCommand(1, "token", parameters)),
        };

    /// <summary>Builds a set of <paramref name="count"/> parameters with distinct short names.</summary>
    /// <param name="count">How many parameters.</param>
    /// <returns>The parameters.</returns>
    private static List<PaymentReturnParameterDto> Parameters(int count) =>
        Enumerable.Range(0, count).Select(i => new PaymentReturnParameterDto($"p{i}", "v")).ToList();

    /// <summary>No parameters, an empty set and a set within every limit pass.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void ASetWithinTheLimits_Passes(string validator)
    {
        var longest = new PaymentReturnParameterDto(
            new string('n', PaymentReturnLimits.MaxNameLength), new string('v', PaymentReturnLimits.MaxValueLength));

        Validate(validator, null).IsValid.Should().BeTrue();
        Validate(validator, []).IsValid.Should().BeTrue();
        Validate(validator, Parameters(PaymentReturnLimits.MaxParameters)).IsValid.Should().BeTrue();
        Validate(validator, [longest]).IsValid.Should().BeTrue();
    }

    /// <summary>One distinct parameter more than the limit is refused with the count sentence.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void TooManyParameters_AreRefused(string validator)
    {
        var result = Validate(validator, Parameters(PaymentReturnLimits.MaxParameters + 1));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("text:PaymentReturnParametersTooMany");
    }

    /// <summary>A repeated name counts once, as it reaches the plugin once, so a dropped duplicate costs no room.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void ADroppedDuplicate_DoesNotCountAgainstTheLimits(string validator)
    {
        var set = Parameters(PaymentReturnLimits.MaxParameters);
        set.Add(new PaymentReturnParameterDto("p0", "again"));

        Validate(validator, set).IsValid.Should().BeTrue();
    }

    /// <summary>A name without text or an absent parameter is ignored, not refused, and does not count either.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void AnEmptyName_IsIgnoredNotRefused(string validator)
    {
        var set = Parameters(PaymentReturnLimits.MaxParameters);
        set.Add(new PaymentReturnParameterDto(string.Empty, "v"));
        set.Add(null!);

        Validate(validator, set).IsValid.Should().BeTrue();
    }

    /// <summary>A name over the limit is refused with the name sentence.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void ATooLongName_IsRefused(string validator)
    {
        var tooLong = new string('n', PaymentReturnLimits.MaxNameLength + 1);

        var result = Validate(validator, [new(tooLong, "v")]);

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("text:PaymentReturnParameterNameTooLong");
    }

    /// <summary>A value over the limit is refused with the value sentence.</summary>
    /// <param name="validator">Which validator.</param>
    [Theory]
    [MemberData(nameof(Validators))]
    public void ATooLongValue_IsRefused(string validator)
    {
        var result = Validate(validator, [new("token", new string('v', PaymentReturnLimits.MaxValueLength + 1))]);

        result.Errors.Should()
            .ContainSingle().Which.ErrorMessage.Should().Be("text:PaymentReturnParameterValueTooLong");
    }
}
