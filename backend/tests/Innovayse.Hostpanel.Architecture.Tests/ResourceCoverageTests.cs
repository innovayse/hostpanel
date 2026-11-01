using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Every error code has a sentence in every language and a status, and every error sentence belongs
/// to a code. <c>IStringLocalizer</c> answers the key on a miss, so without this a customer would read
/// <c>INVOICE_NOT_FOUND</c> and nothing would fail.
/// </summary>
/// <remarks>
/// The validators' keys are held to the same standard: every key a validator looks up has a sentence
/// in every language, and no validation sentence outlives the last validator that used it.
/// </remarks>
public sealed partial class ResourceCoverageTests
{
    /// <summary>The failure statuses <c>rules/api.md</c> allows a code to answer with.</summary>
    private static readonly int[] AllowedFailureStatuses = [400, 401, 402, 403, 404, 409, 410, 413, 422, 423, 429, 500, 502, 503];

    /// <summary>
    /// The codes that answer a status <c>rules/api.md</c> does not list, each with that status and why. An entry is
    /// temporary by construction: <see cref="EveryStatusException_IsStillNeeded"/> fails once the code stops answering it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> StatusExceptions = new Dictionary<string, int>
    {
        // A streaming plugin route cannot be forwarded until #641; 501 says exactly that, and #641 removes the code.
        ["PLUGIN_STREAMING_NOT_SUPPORTED"] = 501,
    };

    /// <summary>The codes the registry declares.</summary>
    private static readonly IReadOnlyList<string> Codes = typeof(HostpanelErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();

    /// <summary>
    /// Each supported locale resolves each code to a non-empty sentence that is not the code itself;
    /// a translated locale must not silently fall back to the neutral English value.
    /// </summary>
    /// <param name="locale">A supported locale.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void EveryCode_InSupportedLocale_HasASentence(string locale)
    {
        var manager = new ResourceManager(typeof(ErrorMessages));
        var culture = CultureInfo.GetCultureInfo(locale);
        var translated = locale != SupportedLanguages.Default;

        var missing = Codes.Where(code =>
        {
            var value = manager.GetString(code, culture);
            return string.IsNullOrWhiteSpace(value)
                   || value == code
                   || (translated && value == manager.GetString(code, CultureInfo.InvariantCulture));
        }).ToList();

        missing.Should().BeEmpty("ErrorMessages.{0}.resx lacks {1}", locale, string.Join(", ", missing));
    }

    /// <summary>No locale's own error resource file holds a key the registry does not declare.</summary>
    /// <param name="locale">A supported locale.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void EverySentence_InSupportedLocale_BelongsToACode(string locale)
    {
        var keys = OwnEntries(typeof(ErrorMessages), locale).Keys;

        keys.Except(Codes).Should().BeEmpty("ErrorMessages resx for {0} has keys without a code", locale);
    }

    /// <summary>
    /// A translated error resource file carries exactly the neutral file's keys, each with its own
    /// non-empty value rather than a copy of the English one.
    /// </summary>
    /// <param name="locale">A translated locale.</param>
    [Theory]
    [InlineData("ru")]
    [InlineData("hy")]
    public void ErrorMessages_InTranslatedLocale_MatchNeutralKeysWithOwnValues(string locale)
    {
        var neutral = OwnEntries(typeof(ErrorMessages), SupportedLanguages.Default);
        var translated = OwnEntries(typeof(ErrorMessages), locale);

        translated.Keys.Should().BeEquivalentTo(
            neutral.Keys,
            "ErrorMessages.{0}.resx must hold the neutral keys",
            locale);

        var untranslated = translated
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value)
                            || entry.Value == neutral.GetValueOrDefault(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        untranslated.Should().BeEmpty("ErrorMessages.{0}.resx has empty or English values", locale);
    }

    /// <summary>
    /// Every slide validation message the validators look up is in the neutral file and, in a
    /// translated locale, carries its own value rather than the English one. A miss would show the
    /// admin the bare key (or the library's English default) in a slide form.
    /// </summary>
    /// <param name="locale">A translated locale.</param>
    [Theory]
    [InlineData("ru")]
    [InlineData("hy")]
    public void SlideValidationMessages_InTranslatedLocale_HaveOwnValues(string locale)
    {
        var neutral = OwnEntries(typeof(ApplicationValidationMessages), SupportedLanguages.Default)
            .Where(entry => entry.Key.StartsWith("Slide", StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        var translated = OwnEntries(typeof(ApplicationValidationMessages), locale);

        neutral.Keys.Should().Contain(["SlideIconNameRequired", "SlideIconNameTooLong", "SlideImageUrlRequired", "SlideImageUrlTooLong"]);

        var untranslated = neutral.Keys
            .Where(key => !translated.TryGetValue(key, out var value)
                          || string.IsNullOrWhiteSpace(value)
                          || value == neutral[key])
            .ToList();

        untranslated.Should().BeEmpty("ApplicationValidationMessages.{0}.resx lacks its own slide sentences", locale);
    }

    /// <summary>
    /// Every code answers with a failure status <c>rules/api.md</c> names, through <see cref="StatusMapping"/>.
    /// A code that reaches no row answers 400, which is still a valid status, so this pins the table
    /// against a row that answers something no client is prepared for.
    /// </summary>
    [Fact]
    public void EveryCode_MapsToAnAllowedFailureStatus()
    {
        var offenders = Codes
            .Where(code => !AllowedFailureStatuses.Contains(StatusMapping.For(code).Status)
                && !(StatusExceptions.TryGetValue(code, out var excepted) && excepted == StatusMapping.For(code).Status))
            .ToList();

        offenders.Should().BeEmpty("every error code answers with a status from rules/api.md");
    }

    /// <summary>An exception to the status list names a code that still exists and still answers that status, so none outlives its reason.</summary>
    [Fact]
    public void EveryStatusException_IsStillNeeded()
    {
        var stale = StatusExceptions
            .Where(entry => !Codes.Contains(entry.Key) || StatusMapping.For(entry.Key).Status != entry.Value
                || AllowedFailureStatuses.Contains(entry.Value))
            .Select(entry => entry.Key)
            .ToList();

        stale.Should().BeEmpty("delete a status exception when its code goes or answers a listed status");
    }

    /// <summary>
    /// Every key a validator looks up (<c>L["Key"]</c> in the Application sources) has its own
    /// non-empty sentence in every supported locale, so no caller is shown a bare key.
    /// </summary>
    /// <param name="locale">A supported locale.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public void EveryValidatorKey_InSupportedLocale_HasASentence(string locale)
    {
        var entries = OwnEntries(typeof(ApplicationValidationMessages), locale);

        var missing = ValidatorKeys()
            .Where(key => !entries.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            .ToList();

        missing.Should().BeEmpty("ApplicationValidationMessages resx for {0} lacks {1}", locale, string.Join(", ", missing));
    }

    /// <summary>
    /// No validation sentence is left behind once the last validator that used its key is gone; a dead
    /// key is translated and reviewed for nothing.
    /// </summary>
    [Fact]
    public void EveryValidationSentence_IsUsedByAValidator()
    {
        var used = ValidatorKeys();

        var unused = OwnEntries(typeof(ApplicationValidationMessages), SupportedLanguages.Default).Keys
            .Where(key => !used.Contains(key))
            .ToList();

        unused.Should().BeEmpty("ApplicationValidationMessages.resx holds keys no validator uses");
    }

    /// <summary>Matches a localizer lookup by a literal key, <c>L["Key"]</c> or <c>L["Key", arg]</c>.</summary>
    /// <returns>The compiled expression; group <c>key</c> is the resource key.</returns>
    [GeneratedRegex("""\bL\["(?<key>\w+)"\s*[,\]]""")]
    private static partial Regex LocalizerLookup();

    /// <summary>Matches an XML documentation line, whose examples are not lookups.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex("^\\s*///.*$", RegexOptions.Multiline)]
    private static partial Regex DocumentationLine();

    /// <summary>The keys the Application sources look up through a localizer.</summary>
    /// <returns>The distinct keys.</returns>
    private static HashSet<string> ValidatorKeys()
    {
        var backendRoot = UseCaseShapeTests.FindBackendRoot(AppContext.BaseDirectory);
        var applicationRoot = Path.Combine(backendRoot, "src", "Innovayse.Hostpanel.Application");

        return Directory
            .EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => LocalizerLookup().Matches(DocumentationLine().Replace(File.ReadAllText(path), string.Empty)))
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The entries a locale's own resource file holds, without parent-culture fallback. The default
    /// locale is the neutral file.
    /// </summary>
    /// <param name="resourceType">The marker type naming the resource set.</param>
    /// <param name="locale">A supported locale.</param>
    /// <returns>Key to value, empty when the locale has no file of its own.</returns>
    private static Dictionary<string, string?> OwnEntries(Type resourceType, string locale)
    {
        var culture = locale == SupportedLanguages.Default
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(locale);
        var set = new ResourceManager(resourceType).GetResourceSet(culture, true, false);

        return set is null
            ? []
            : set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => entry.Value as string);
    }
}
