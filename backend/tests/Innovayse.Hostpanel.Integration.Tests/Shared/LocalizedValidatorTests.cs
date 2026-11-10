using Innovayse.Hostpanel.Application.Common.Validators;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// A validator deriving from <see cref="LocalizedValidator{T}"/> takes its field messages from the
/// localizer it was given, by key, so the sentence is the resource's and never a literal.
/// </summary>
/// <remarks>No mock of our own code: <see cref="EchoLocalizer{TResource}"/> stands in
/// for the framework's <see cref="IStringLocalizer"/>, not for anything this product owns.</remarks>
public sealed class LocalizedValidatorTests
{
    /// <summary>A refused field carries the sentence the localizer holds under the rule's key.</summary>
    [Fact]
    public void Validate_RefusedField_UsesTheLocalizedSentence()
    {
        var validator = new NameProbeValidator(new EchoLocalizer<NameProbe>());

        var result = validator.Validate(new NameProbe(string.Empty));

        var error = Assert.Single(result.Errors);
        Assert.Equal("text:NameRequired", error.ErrorMessage);
    }

    /// <summary>A valid message passes with no errors.</summary>
    [Fact]
    public void Validate_ValidMessage_Passes()
    {
        var validator = new NameProbeValidator(new EchoLocalizer<NameProbe>());

        var result = validator.Validate(new NameProbe("x"));

        Assert.True(result.IsValid);
    }
}
