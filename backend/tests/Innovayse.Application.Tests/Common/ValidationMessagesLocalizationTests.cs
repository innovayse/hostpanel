namespace Innovayse.Application.Tests.Common;

using System.Globalization;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common.Options;
using Innovayse.Application.Resources;
using Innovayse.Application.Support.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Proves a request that asks for Russian or Armenian is answered in Russian or Armenian.
/// <para>
/// The portal ships three languages and the backend used to ship one. The frontend papered over
/// that with a mapping table of its own (<c>client/utils/portalErrorMessages.ts</c>) covering five
/// codes, so every other refusal reached a Russian or Armenian customer in English. That table is
/// gone and the sentence now comes from
/// <c>Innovayse.Application/Resources/ValidationMessages*.resx</c>, selected by the culture
/// <c>UseRequestLocalization</c> reads off <c>Accept-Language</c>.
/// </para>
/// <para>
/// <b>This is the test that stops it regressing silently.</b> Nothing throws when the resource
/// wiring breaks: <see cref="IStringLocalizer"/> answers a missing key with the key itself, so a
/// renamed folder, a moved marker type or a <c>ResourcesPath</c> set by mistake would turn every
/// refusal into <c>ClientProfileNotFound</c> on the screen and no build or startup would object.
/// Assertions here are on real lookups for that reason.
/// </para>
/// </summary>
public sealed class ValidationMessagesLocalizationTests
{
    /// <summary>The language tag a Russian-speaking customer's browser sends.</summary>
    private const string Russian = "ru";

    /// <summary>The language tag an Armenian-speaking customer's browser sends.</summary>
    private const string Armenian = "hy";

    /// <summary>
    /// A key that is present in the neutral file and deliberately absent from <c>ru</c> and
    /// <c>hy</c> -- one of the still-untranslated service messages. Used to pin the fallback.
    /// </summary>
    private const string UntranslatedKey = "ServiceNotProvisioned";

    /// <summary>
    /// Builds the localizer the API resolves, without a web host.
    /// </summary>
    /// <returns>The localizer over <see cref="ValidationMessages"/>.</returns>
    /// <remarks>
    /// <c>AddLocalization()</c> with no <c>ResourcesPath</c> is exactly what <c>Program.cs</c>
    /// calls, so a change to either side that the other does not follow fails here.
    /// </remarks>
    private static IStringLocalizer<ValidationMessages> BuildLocalizer()
    {
        var services = new ServiceCollection();

        // ResourceManagerStringLocalizerFactory takes an ILoggerFactory, which the API has from
        // the host and a bare ServiceCollection does not. Nothing here asserts on log output, so
        // the null factory is the whole of what is needed to stand the real factory up.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<ValidationMessages>>();
    }

    /// <summary>
    /// Looks a key up as if a request had arrived carrying the given <c>Accept-Language</c>.
    /// </summary>
    /// <param name="languageTag">The culture the request asked for.</param>
    /// <param name="key">Resource key to resolve.</param>
    /// <param name="arguments">Values substituted into the message's placeholders.</param>
    /// <returns>The sentence the caller would be shown.</returns>
    /// <remarks>
    /// The ambient culture is restored in a <c>finally</c>: xUnit reuses threads across tests in a
    /// collection, and a leaked <c>CurrentUICulture</c> would make an unrelated test pass or fail
    /// depending on the order it ran in.
    /// </remarks>
    private static string Resolve(string languageTag, string key, params object[] arguments)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(languageTag);
            var localizer = BuildLocalizer();
            return arguments.Length == 0 ? localizer[key].Value : localizer[key, arguments].Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// A Russian caller reads the refusal in Russian, not in English.
    /// </summary>
    [Fact]
    public void RussianCallerGetsTheRussianSentence()
    {
        var message = Resolve(Russian, ClientProfileNotFoundException.MessageKey);

        Assert.NotEqual(ClientProfileNotFoundException.PublicMessage, message);
        Assert.NotEqual(ClientProfileNotFoundException.MessageKey, message);
        Assert.Contains("клиентск", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An Armenian caller reads the refusal in Armenian. Asserted on the Armenian block of the
    /// code chart rather than on a copy of the sentence, so rewording the translation does not
    /// break the test while replacing it with English still does.
    /// </summary>
    [Fact]
    public void ArmenianCallerGetsTheArmenianSentence()
    {
        var message = Resolve(Armenian, ClientProfileNotFoundException.MessageKey);

        Assert.NotEqual(ClientProfileNotFoundException.PublicMessage, message);
        Assert.All(
            message.Where(char.IsLetter).Take(1),
            first => Assert.InRange(first, '԰', '֏'));
    }

    /// <summary>
    /// Every language the portal ships answers the rate-limit refusal with something other than
    /// the key, which is what a broken resource lookup would return.
    /// </summary>
    [Fact]
    public void EverySupportedLocaleResolvesTheRateLimitSentence()
    {
        foreach (var locale in LocaleOptions.SupportedLocales)
        {
            var message = Resolve(locale, "RateLimited");

            Assert.NotEqual("RateLimited", message);
            Assert.NotEmpty(message);
        }
    }

    /// <summary>
    /// The two languages disagree with each other. A resource set that failed to load would give
    /// all three the same English string and every other assertion here could still pass.
    /// </summary>
    [Fact]
    public void ThreeLocalesGiveThreeDifferentSentences()
    {
        var english = Resolve("en", ContactMessageNotSentException.MessageKey);
        var russian = Resolve(Russian, ContactMessageNotSentException.MessageKey);
        var armenian = Resolve(Armenian, ContactMessageNotSentException.MessageKey);

        Assert.Equal(ContactMessageNotSentException.PublicMessage, english);
        Assert.NotEqual(english, russian);
        Assert.NotEqual(english, armenian);
        Assert.NotEqual(russian, armenian);
    }

    /// <summary>
    /// A key the translators have not reached yet falls back to English rather than to the key
    /// name. This is the behaviour that lets the still-untranslated admin messages ship.
    /// </summary>
    [Fact]
    public void UntranslatedKeyFallsBackToEnglish()
    {
        var english = Resolve("en", UntranslatedKey, 42);
        var armenian = Resolve(Armenian, UntranslatedKey, 42);

        Assert.Equal(english, armenian);
        Assert.Contains("42", armenian, StringComparison.Ordinal);
    }

    /// <summary>
    /// A translated message keeps the value the handler passed in. The Armenian and Russian
    /// strings carry <c>{0}</c> in a different position from the English one, which is exactly the
    /// mistake a translator makes and a reader never notices.
    /// </summary>
    [Fact]
    public void PlaceholdersSurviveTranslation()
    {
        foreach (var locale in LocaleOptions.SupportedLocales)
        {
            var message = Resolve(locale, "UserAlreadyLinked", "jane@example.com");

            Assert.Contains("jane@example.com", message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A key nothing defines resolves to itself. Pinned deliberately: it is why a missing
    /// translation is visible on screen instead of silently English, and why this test class
    /// asserts on real lookups rather than trusting that no exception means it worked.
    /// </summary>
    [Fact]
    public void MissingKeyResolvesToTheKeyItself()
    {
        Assert.Equal("NoSuchResourceKey", Resolve(Armenian, "NoSuchResourceKey"));
    }
}
