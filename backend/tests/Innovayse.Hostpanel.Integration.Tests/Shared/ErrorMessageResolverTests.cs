using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The resolver <c>BaseApiController.MapFailure</c> uses turns a code into the sentence in the
/// request's language, and never hands a caller the bare code.
/// </summary>
/// <remarks>No mock of our own code: the localizer is built from the real
/// DI registration the API uses.</remarks>
public sealed class ErrorMessageResolverTests
{
    /// <summary>A code the registry does not declare, so the resource set has no entry for it.</summary>
    private const string UnknownCode = "NO_SUCH_CODE_ANYWHERE";

    /// <summary>
    /// Builds the resolver over the localizer the API registers, without a web host.
    /// </summary>
    /// <returns>The resolver under test.</returns>
    private static ErrorMessageResolver BuildResolver()
    {
        var services = new ServiceCollection();

        // The localizer factory takes an ILoggerFactory, which a bare ServiceCollection lacks.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLocalization();
        var localizer = services.BuildServiceProvider().GetRequiredService<IStringLocalizer<ErrorMessages>>();
        return new ErrorMessageResolver(localizer);
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the UI culture set to <paramref name="culture"/>, as the
    /// request-localization middleware would, and restores the previous culture afterwards.
    /// </summary>
    /// <param name="culture">The language tag to run under.</param>
    /// <param name="action">The lookup to run.</param>
    /// <returns>What <paramref name="action"/> returned.</returns>
    private static string InCulture(string culture, Func<string> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Each language answers its own sentence, and none of them is the code.</summary>
    [Fact]
    public void Resolve_KnownCode_AnswersInTheRequestLanguage()
    {
        var resolver = BuildResolver();

        var en = InCulture("en", () => resolver.Resolve(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED));
        var ru = InCulture("ru", () => resolver.Resolve(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED));
        var hy = InCulture("hy", () => resolver.Resolve(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED));

        Assert.Equal("Something went wrong on the server and the request was not completed. Please try again. If it keeps happening, check the server log or contact the site administrator.", en);
        Assert.Equal("На сервере произошла ошибка, запрос не выполнен. Повторите попытку. Если ошибка повторяется, проверьте журнал сервера или обратитесь к администратору сайта.", ru);
        Assert.Equal("Սերվերում սխալ տեղի ունեցավ, հարցումը չկատարվեց։ Փորձեք կրկին։ Եթե սխալը կրկնվում է, ստուգեք սերվերի մատյանը կամ դիմեք կայքի ադմինիստրատորին։", hy);
    }

    /// <summary>A code with no entry answers the generic sentence in the request language, never the code.</summary>
    [Fact]
    public void Resolve_UnknownCode_FallsBackToTheGenericSentence()
    {
        var resolver = BuildResolver();

        var ru = InCulture("ru", () => resolver.Resolve(UnknownCode));

        Assert.Equal("На сервере произошла ошибка, запрос не выполнен. Повторите попытку. Если ошибка повторяется, проверьте журнал сервера или обратитесь к администратору сайта.", ru);
    }
}
