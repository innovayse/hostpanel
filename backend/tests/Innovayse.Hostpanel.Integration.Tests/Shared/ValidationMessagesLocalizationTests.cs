using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// Proves a request that asks for Russian, Armenian, German, Spanish or French is answered in that language.
/// <para>
/// The client app ships several languages and does not translate error codes itself: the sentence
/// comes from
/// <c>Innovayse.Hostpanel.Application/Resources/ApplicationValidationMessages*.resx</c>, selected by the culture
/// <c>UsePlatformLocalization</c> reads off <c>X-Language</c>.
/// </para>
/// <para>
/// <b>This is the test that stops it breaking silently.</b> Nothing throws when the resource
/// wiring breaks: <see cref="IStringLocalizer"/> answers a missing key with the key itself, so a
/// renamed folder, a moved marker type or a <c>ResourcesPath</c> set by mistake would turn every
/// refusal into <c>DomainNotFound</c> on the screen and no build or startup would object.
/// Assertions here are on real lookups for that reason.
/// </para>
/// </summary>
/// <remarks>No mock of our own code: the localizer is built from the real
/// DI registration the API uses, with no database required.</remarks>
public sealed class ValidationMessagesLocalizationTests
{
    /// <summary>The language tag a Russian-speaking customer's browser sends.</summary>
    private const string Russian = "ru";

    /// <summary>The language tag an Armenian-speaking customer's browser sends.</summary>
    private const string Armenian = "hy";

    /// <summary>The language tag a German-speaking customer's browser sends.</summary>
    private const string German = "de";

    /// <summary>The language tag a Spanish-speaking customer's browser sends.</summary>
    private const string Spanish = "es";

    /// <summary>
    /// A language tag no resource file is shipped for. Every key resolves through the culture
    /// fallback to the neutral (English) file; every supported language carries every key, so only a
    /// language with no file exercises the fallback.
    /// </summary>
    private const string LocaleWithoutTranslations = "it";

    /// <summary>A key of the neutral file whose English sentence takes a length limit.</summary>
    private const string FallbackKey = "ProductNameTooLong";

    /// <summary>
    /// Builds the localizer the API resolves, without a web host.
    /// </summary>
    /// <typeparam name="TResource">The marker type naming the resource set.</typeparam>
    /// <returns>The localizer over <typeparamref name="TResource"/>.</returns>
    /// <remarks>
    /// <c>AddLocalization()</c> with no <c>ResourcesPath</c> is exactly what <c>Program.cs</c>
    /// calls, so a change to either side that the other does not follow fails here.
    /// </remarks>
    private static IStringLocalizer<TResource> BuildLocalizer<TResource>()
    {
        var services = new ServiceCollection();

        // ResourceManagerStringLocalizerFactory takes an ILoggerFactory, which the API has from
        // the host and a bare ServiceCollection does not. Nothing here asserts on log output, so
        // the null factory is the whole of what is needed to stand the real factory up.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<TResource>>();
    }

    /// <summary>
    /// Looks a key up as if a request had arrived carrying the given <c>X-Language</c>.
    /// </summary>
    /// <typeparam name="TResource">The marker type naming the resource set.</typeparam>
    /// <param name="languageTag">The culture the request asked for.</param>
    /// <param name="key">Resource key to resolve.</param>
    /// <param name="arguments">Values substituted into the message's placeholders.</param>
    /// <returns>The sentence the caller would be shown.</returns>
    /// <remarks>
    /// The ambient culture is restored in a <c>finally</c>: xUnit reuses threads across tests in a
    /// collection, and a leaked <c>CurrentUICulture</c> would make an unrelated test pass or fail
    /// depending on the order it ran in.
    /// </remarks>
    private static string Resolve<TResource>(string languageTag, string key, params object[] arguments)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(languageTag);
            var localizer = BuildLocalizer<TResource>();
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
        var message = Resolve<ApplicationValidationMessages>(Russian, "PaymentMethodRequired");

        Assert.NotEqual("Select a payment method.", message);
        Assert.NotEqual("PaymentMethodRequired", message);
        Assert.Contains("способ", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An Armenian caller reads the refusal in Armenian. Asserted on the Armenian block of the
    /// code chart rather than on a copy of the sentence, so rewording the translation does not
    /// break the test while replacing it with English still does.
    /// </summary>
    [Fact]
    public void ArmenianCallerGetsTheArmenianSentence()
    {
        var message = Resolve<ApplicationValidationMessages>(Armenian, "PaymentMethodRequired");

        Assert.NotEqual("Select a payment method.", message);
        Assert.All(
            message.Where(char.IsLetter).Take(1),
            first => Assert.InRange(first, '԰', '֏'));
    }

    /// <summary>
    /// A German caller reads the refusal in German, not in English.
    /// </summary>
    [Fact]
    public void GermanCallerGetsTheGermanSentence()
    {
        var message = Resolve<ApplicationValidationMessages>(German, "PaymentMethodRequired");

        Assert.NotEqual("Select a payment method.", message);
        Assert.NotEqual("PaymentMethodRequired", message);
    }

    /// <summary>
    /// A Spanish caller reads the refusal in Spanish, not in English.
    /// </summary>
    [Fact]
    public void SpanishCallerGetsTheSpanishSentence()
    {
        var message = Resolve<ApplicationValidationMessages>(Spanish, "PaymentMethodRequired");

        Assert.NotEqual("Select a payment method.", message);
        Assert.NotEqual("PaymentMethodRequired", message);
    }

    /// <summary>
    /// The two languages disagree with each other. A resource set that failed to load would give
    /// all three the same English string and every other assertion here could still pass.
    /// </summary>
    [Fact]
    public void EveryLocaleGivesItsOwnSentence()
    {
        const string key = "PaymentMethodRequired";

        var english = Resolve<ApplicationValidationMessages>("en", key);
        var russian = Resolve<ApplicationValidationMessages>(Russian, key);
        var armenian = Resolve<ApplicationValidationMessages>(Armenian, key);
        var german = Resolve<ApplicationValidationMessages>(German, key);
        var spanish = Resolve<ApplicationValidationMessages>(Spanish, key);

        Assert.Equal("Select a payment method.", english);
        Assert.Equal(5, new[] { english, russian, armenian, german, spanish }.Distinct().Count());
    }

    /// <summary>
    /// A caller whose language has no translations falls back to English rather than to the key
    /// name. This is the behaviour that lets a key ship before its translations do.
    /// </summary>
    [Fact]
    public void LocaleWithoutTranslationsFallsBackToEnglish()
    {
        var english = Resolve<ApplicationValidationMessages>("en", FallbackKey, 42);
        var fallback = Resolve<ApplicationValidationMessages>(LocaleWithoutTranslations, FallbackKey, 42);

        Assert.Equal(english, fallback);
        Assert.NotEqual(FallbackKey, fallback);
        Assert.Contains("42", fallback, StringComparison.Ordinal);
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
            var message = Resolve<ApplicationValidationMessages>(locale, "ProductNameTooLong", 64);

            Assert.Contains("64", message, StringComparison.Ordinal);
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
        Assert.Equal("NoSuchResourceKey", Resolve<ApplicationValidationMessages>(Armenian, "NoSuchResourceKey"));
    }
}
