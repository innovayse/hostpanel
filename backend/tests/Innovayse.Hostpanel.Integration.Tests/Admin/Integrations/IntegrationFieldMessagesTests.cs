using System.Globalization;
using System.Reflection;
using System.Resources;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Resources;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// Pins that every per-field sentence the integration save can put in <c>errors[key]</c> has its own value in
/// English, Russian and Armenian, so a field refusal is never hardcoded English or a bare resource key.
/// </summary>
public sealed class IntegrationFieldMessagesTests
{
    /// <summary>The frame and the "not declared" sentence the handler words besides the rules' expectations.</summary>
    private static readonly string[] HandlerKeys = ["IntegrationFieldExpected", "IntegrationFieldUnknown"];

    /// <summary>Gets every resource key the rules and the handler use for a field sentence.</summary>
    /// <returns>The keys.</returns>
    private static IReadOnlyList<string> AllKeys()
    {
        var expectations = typeof(IntegrationFieldValueRules)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);
        return HandlerKeys.Concat(expectations).ToList();
    }

    /// <summary>Every field sentence exists in each language, and a translated one differs from the English one.</summary>
    /// <param name="locale">A supported locale.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void EveryFieldSentence_HasAValueInEachLanguage(string locale)
    {
        var manager = new ResourceManager(typeof(ApplicationValidationMessages));
        var culture = CultureInfo.GetCultureInfo(locale);

        AllKeys().Should().HaveCount(11);
        foreach (var key in AllKeys())
        {
            var value = manager.GetString(key, culture);

            value.Should().NotBeNullOrWhiteSpace($"{key} is missing in {locale}");
            if (locale != "en")
            {
                value.Should().NotBe(manager.GetString(key, CultureInfo.GetCultureInfo("en")), $"{key} is untranslated in {locale}");
            }
        }
    }

    /// <summary>The frame and the bounded expectations keep the placeholders their arguments fill, in every language.</summary>
    /// <param name="locale">A supported locale.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void FieldSentences_KeepTheirPlaceholders(string locale)
    {
        var manager = new ResourceManager(typeof(ApplicationValidationMessages));
        var culture = CultureInfo.GetCultureInfo(locale);

        manager.GetString("IntegrationFieldExpected", culture).Should().Contain("{0}");
        manager.GetString(IntegrationFieldValueRules.ExpectSelect, culture).Should().Contain("{0}");
        manager.GetString(IntegrationFieldValueRules.ExpectNumberMin, culture).Should().Contain("{0}");
        manager.GetString(IntegrationFieldValueRules.ExpectNumberMax, culture).Should().Contain("{0}");
        manager.GetString(IntegrationFieldValueRules.ExpectNumberRange, culture).Should().Contain("{0}").And.Contain("{1}");
    }
}
