namespace Innovayse.Application.Tests.Auth;

using FluentValidation.TestHelper;
using Innovayse.Application.Auth.Commands.Login;
using Xunit;

/// <summary>Unit tests for <see cref="LoginValidator"/>.</summary>
public class LoginValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly LoginValidator _validator = new();

    /// <summary>Valid credentials should pass validation.</summary>
    [Fact]
    public void ValidCredentials_ShouldPassValidation()
    {
        var result = _validator.TestValidate(new LoginCommand("user@example.com", "secret"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Empty email should fail.</summary>
    [Fact]
    public void EmptyEmail_ShouldFail()
    {
        var result = _validator.TestValidate(new LoginCommand("", "secret"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>Empty password should fail.</summary>
    [Fact]
    public void EmptyPassword_ShouldFail()
    {
        var result = _validator.TestValidate(new LoginCommand("user@example.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
