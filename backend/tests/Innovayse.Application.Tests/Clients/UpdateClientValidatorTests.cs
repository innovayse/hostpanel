namespace Innovayse.Application.Tests.Clients;

using FluentValidation.TestHelper;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Xunit;

/// <summary>Unit tests for <see cref="UpdateClientValidator"/>.</summary>
public class UpdateClientValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly UpdateClientValidator _validator = new();

    /// <summary>Creates a valid command with sensible defaults for all fields.</summary>
    private static UpdateClientCommand MakeValidCommand(
        int clientId = 1,
        string? email = "john@example.com",
        string firstName = "John",
        string lastName = "Doe",
        string? companyName = null,
        string? phone = null,
        string? street = null,
        string? address2 = null,
        string? city = null,
        string? state = null,
        string? postCode = null,
        string? country = null,
        string? currency = null,
        string? paymentMethod = null,
        string? billingContact = null,
        string? adminNotes = null,
        string? status = null) =>
        new(
            clientId, email, firstName, lastName, companyName, phone,
            street, address2, city, state, postCode, country,
            currency, paymentMethod, billingContact, adminNotes,
            true, true, true, true, true, true,
            true, true, false, false, false, false, true, true,
            status);

    /// <summary>Valid command should pass.</summary>
    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = MakeValidCommand();
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>ClientId 0 should fail.</summary>
    [Fact]
    public void ZeroClientId_ShouldFail()
    {
        var cmd = MakeValidCommand(clientId: 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    /// <summary>Country code with wrong length should fail.</summary>
    [Fact]
    public void WrongLengthCountry_ShouldFail()
    {
        var cmd = MakeValidCommand(country: "USA");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Country);
    }

    /// <summary>Empty FirstName should fail.</summary>
    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = MakeValidCommand(firstName: "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
