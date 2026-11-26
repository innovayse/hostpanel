using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Pins which sentence <see cref="AddExistingServiceValidator"/> asks for on each field of the add existing service command, and that
/// the two confirmation flags are never a reason to refuse. Runs without a database.
/// </summary>
public sealed class AddExistingServiceValidatorTests
{
    /// <summary>Answers each message key with a marked copy of the key, so no resource file is needed.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>The validator under test.</summary>
    private static readonly AddExistingServiceValidator Validator = new(Localizer);

    /// <summary>A complete command passes, with the confirmations off and on.</summary>
    /// <param name="confirmed">Whether both confirmations are set.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteCommand_Passes(bool confirmed)
    {
        var command = Valid() with { ConfirmUnverified = confirmed, ConfirmSharedDomain = confirmed };

        Validator.Validate(command).IsValid.Should().BeTrue();
    }

    /// <summary>A missing username is refused with the username sentence, under its field.</summary>
    [Fact]
    public void EmptyUsername_IsRefusedWithTheUsernameSentence()
    {
        var result = Validator.Validate(Valid() with { Username = string.Empty });

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(AddExistingServiceCommand.Username));
        error.ErrorMessage.Should().Be("text:ServiceUsernameRequired");
    }

    /// <summary>A domain longer than a host name can be is refused with the domain length sentence, under its field.</summary>
    [Fact]
    public void DomainOver253Characters_IsRefusedWithTheLengthSentence()
    {
        var result = Validator.Validate(Valid() with { Domain = new string('a', 254) });

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(AddExistingServiceCommand.Domain));
        error.ErrorMessage.Should().Be("text:HostingAccountDomainTooLong");
    }

    /// <summary>A server id that is not positive is refused with the server sentence, under its field.</summary>
    [Fact]
    public void NonPositiveServerId_IsRefusedWithTheServerSentence()
    {
        var result = Validator.Validate(Valid() with { ServerId = 0 });

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(AddExistingServiceCommand.ServerId));
        error.ErrorMessage.Should().Be("text:HostingAccountServerIdInvalid");
    }

    /// <summary>Builds a command every rule accepts.</summary>
    /// <returns>A valid command.</returns>
    private static AddExistingServiceCommand Valid()
    {
        return new AddExistingServiceCommand(1, 2, BillingCycle.Annual, 49.99m, 3, "site.example", "siteuser", null);
    }
}
