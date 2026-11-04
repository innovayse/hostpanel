using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Domain.Settings.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>Pins what <see cref="IntegrationFieldValueRules"/> accepts and refuses for each field type.</summary>
public sealed class IntegrationFieldValueRulesTests
{
    /// <summary>Builds a field definition for a test.</summary>
    /// <param name="type">The field type.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <param name="options">The select options.</param>
    /// <param name="min">The number minimum.</param>
    /// <param name="max">The number maximum.</param>
    /// <returns>The definition.</returns>
    private static FieldDefinitionDto Field(
        IntegrationFieldType type, bool required = false, string[]? options = null, decimal? min = null, decimal? max = null)
    {
        return new FieldDefinitionDto("field", "Field", type, required, options, min, max);
    }

    /// <summary>An absolute http or https address is accepted as written.</summary>
    /// <param name="value">The address.</param>
    [Theory]
    [InlineData("https://example.test")]
    [InlineData("http://example.test:8080/path")]
    public void Url_AbsoluteHttpOrHttps_IsAccepted(string value)
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Url), value);

        check.IsValid.Should().BeTrue();
        check.Text.Should().Be(value);
    }

    /// <summary>A relative address, another scheme or plain text is refused with the address expectation key.</summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("example.test")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.test")]
    [InlineData("javascript:alert(1)")]
    public void Url_NotAbsoluteHttp_IsRefused(string value)
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Url), value);

        check.IsValid.Should().BeFalse();
        check.Text.Should().Be(IntegrationFieldValueRules.ExpectUrl);
    }

    /// <summary>A number inside the bounds is accepted, with an invariant decimal point.</summary>
    /// <param name="value">The number.</param>
    [Theory]
    [InlineData("5")]
    [InlineData("1")]
    [InlineData("10")]
    [InlineData("2.5")]
    public void Number_InsideBounds_IsAccepted(string value)
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Number, min: 1, max: 10), value);

        check.IsValid.Should().BeTrue();
        check.Text.Should().Be(value);
    }

    /// <summary>A number outside the bounds, a comma decimal or text is refused.</summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("11")]
    [InlineData("2,5")]
    [InlineData("abc")]
    [InlineData("1e3")]
    public void Number_OutsideBoundsOrMalformed_IsRefused(string value)
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Number, min: 1, max: 10), value);

        check.IsValid.Should().BeFalse();
        check.Text.Should().Be(IntegrationFieldValueRules.ExpectNumberRange);
        check.Arguments.Should().Equal("1", "10");
    }

    /// <summary>A select value is stored in the spelling the field declares.</summary>
    [Fact]
    public void Select_CaseVariant_IsStoredInTheDeclaredSpelling()
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Select, options: ["Live", "Test"]), "live");

        check.IsValid.Should().BeTrue();
        check.Text.Should().Be("Live");
    }

    /// <summary>A select value that is not an option is refused, naming the options.</summary>
    [Fact]
    public void Select_UnknownOption_IsRefused()
    {
        var check = IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Select, options: ["Live", "Test"]), "staging");

        check.IsValid.Should().BeFalse();
        check.Text.Should().Be(IntegrationFieldValueRules.ExpectSelect);
        check.Arguments.Should().Equal("Live, Test");
    }

    /// <summary>
    /// An empty value is accepted for every non-secret type, required or not: a required field is enforced when the
    /// integration is activated, so a half-configured or disabled integration can still be saved.
    /// </summary>
    /// <param name="type">The field type.</param>
    /// <param name="required">Whether the field is required.</param>
    [Theory]
    [InlineData(IntegrationFieldType.Url, true)]
    [InlineData(IntegrationFieldType.Url, false)]
    [InlineData(IntegrationFieldType.Number, true)]
    [InlineData(IntegrationFieldType.Number, false)]
    [InlineData(IntegrationFieldType.Select, true)]
    [InlineData(IntegrationFieldType.Select, false)]
    [InlineData(IntegrationFieldType.Toggle, true)]
    [InlineData(IntegrationFieldType.Toggle, false)]
    [InlineData(IntegrationFieldType.Text, true)]
    [InlineData(IntegrationFieldType.Text, false)]
    public void Empty_IsAcceptedForEveryTypeWhetherOrNotRequired(IntegrationFieldType type, bool required)
    {
        var field = Field(type, required, ["A"], 1, 10);

        foreach (var empty in new string?[] { null, string.Empty, "   " })
        {
            var check = IntegrationFieldValueRules.Check(field, empty);

            check.IsValid.Should().BeTrue();
            if (type is not (IntegrationFieldType.Text or IntegrationFieldType.TextArea))
            {
                check.Text.Should().BeEmpty();
            }
        }
    }

    /// <summary>A toggle is stored lowercased; anything but true or false is refused.</summary>
    [Fact]
    public void Toggle_StoresLowercaseAndRefusesOtherText()
    {
        var field = Field(IntegrationFieldType.Toggle);

        IntegrationFieldValueRules.Check(field, "TRUE").Text.Should().Be("true");
        IntegrationFieldValueRules.Check(field, "False").Text.Should().Be("false");
        IntegrationFieldValueRules.Check(field, "yes").IsValid.Should().BeFalse();
        IntegrationFieldValueRules.Check(field, "1").IsValid.Should().BeFalse();
    }

    /// <summary>A secret or password must be non-blank; a blank one means unchanged and is never a value.</summary>
    /// <param name="type">The secret kind.</param>
    [Theory]
    [InlineData(IntegrationFieldType.Secret)]
    [InlineData(IntegrationFieldType.Password)]
    public void Secret_RequiresANonBlankValue(IntegrationFieldType type)
    {
        IntegrationFieldValueRules.Check(Field(type), "  ").IsValid.Should().BeFalse();
        IntegrationFieldValueRules.Check(Field(type), null).IsValid.Should().BeFalse();
        IntegrationFieldValueRules.Check(Field(type), " keep spaces ").Text.Should().Be(" keep spaces ");
    }

    /// <summary>Text and text area values are stored as written, an absent one as empty.</summary>
    [Fact]
    public void Text_IsStoredAsWritten()
    {
        IntegrationFieldValueRules.Check(Field(IntegrationFieldType.Text), " any thing ").Text.Should().Be(" any thing ");
        IntegrationFieldValueRules.Check(Field(IntegrationFieldType.TextArea), null).Text.Should().BeEmpty();
    }
}
