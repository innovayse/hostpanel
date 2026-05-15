namespace Innovayse.Application.Tests.Auth;

using FluentValidation.TestHelper;
using Innovayse.Application.Auth.Commands.Register;
using Xunit;

/// <summary>Unit tests for <see cref="RegisterValidator"/>.</summary>
public class RegisterValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly RegisterValidator _validator = new();

    /// <summary>Valid command should pass validation.</summary>
    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var result = _validator.TestValidate(new RegisterCommand("user@example.com", "Password123!", "John", "Doe"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Empty email should fail validation.</summary>
    [Fact]
    public void EmptyEmail_ShouldFailValidation()
    {
        var result = _validator.TestValidate(new RegisterCommand("", "Password123!", "John", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>Short password should fail validation.</summary>
    [Fact]
    public void ShortPassword_ShouldFailValidation()
    {
        var result = _validator.TestValidate(new RegisterCommand("user@example.com", "abc", "John", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    /// <summary>Invalid email format should fail validation.</summary>
    [Fact]
    public void InvalidEmail_ShouldFailValidation()
    {
        var result = _validator.TestValidate(new RegisterCommand("not-an-email", "Password123!", "John", "Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
