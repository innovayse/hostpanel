using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>Pins the outcome shapes every handler returns, so a refactor cannot silently drop a field.</summary>
public sealed class ResultTests
{
    /// <summary>A successful typed result carries the value and no error information.</summary>
    [Fact]
    public void SuccessCarriesTheValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    /// <summary>A code-only failure sets <c>ErrorCode</c> and leaves <c>Error</c> null or empty.</summary>
    [Fact]
    public void FailureCodeSetsErrorCodeWithoutAnError()
    {
        var result = Result.Failure(string.Empty, "NOT_FOUND");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(SharedErrorCodes.NotFound);
        result.Error.Should().BeNullOrEmpty();
    }

    /// <summary>A message-and-code failure keeps both readable on the result.</summary>
    [Fact]
    public void FailureKeepsBothMessageAndCode()
    {
        var result = Result.Failure("Invoice not found.", "INVOICE_NOT_FOUND");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invoice not found.");
        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Field-level errors set on a failure round-trip through the <c>Errors</c> dictionary.</summary>
    [Fact]
    public void ErrorsDictionaryRoundTrips()
    {
        var result = Result.Failure("Price must be greater than 0.", "VALIDATION_ERROR", "Price");

        result.Errors.Should().NotBeNull();
        result.Errors.Should().ContainKey("Price");
        result.Errors!["Price"].Should().ContainSingle().Which.Should().Be("Price must be greater than 0.");
    }

    /// <summary>
    /// <c>FailureCode</c> puts the code in the code slot and leaves the message empty: the sentence
    /// comes from the resources at the boundary.
    /// </summary>
    [Fact]
    public void FailureCodeCarriesTheCodeAndNoMessage()
    {
        var result = Result.FailureCode("CURRENCY_NOT_FOUND");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        result.Error.Should().BeNull();
    }

    /// <summary>The typed twin behaves the same and carries no value.</summary>
    [Fact]
    public void FailureCodeOfTCarriesTheCodeAndNoValue()
    {
        var result = Result<int>.FailureCode("CURRENCY_NOT_FOUND");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        result.Error.Should().BeNull();
        result.Value.Should().Be(0);
    }
}
