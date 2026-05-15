namespace Innovayse.Application.Tests.Clients;

using FluentValidation.TestHelper;
using Innovayse.Application.Clients.Commands.CreateClient;
using Xunit;

/// <summary>Unit tests for <see cref="CreateClientValidator"/>.</summary>
public class CreateClientValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly CreateClientValidator _validator = new();

    /// <summary>Valid command should pass.</summary>
    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new CreateClientCommand("user-1", "john@example.com", "John", "Doe");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Empty UserId should fail.</summary>
    [Fact]
    public void EmptyUserId_ShouldFail()
    {
        var cmd = new CreateClientCommand("", "john@example.com", "John", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.UserId);
    }

    /// <summary>Invalid email should fail.</summary>
    [Fact]
    public void InvalidEmail_ShouldFail()
    {
        var cmd = new CreateClientCommand("user-1", "not-an-email", "John", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>Empty FirstName should fail.</summary>
    [Fact]
    public void EmptyFirstName_ShouldFail()
    {
        var cmd = new CreateClientCommand("user-1", "john@example.com", "", "Doe");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
