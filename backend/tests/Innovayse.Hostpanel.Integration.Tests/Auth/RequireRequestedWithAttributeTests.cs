using Innovayse.Hostpanel.API.Auth;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <see cref="RequireRequestedWithAttribute"/>: a request that changes state needs the header or a bearer token whatever
/// credential it rode on, and a read never does. The cookie path is also covered over HTTP by the plugin door tests; this
/// is the filter on its own, for the request that carries neither a cookie nor a bearer.
/// </summary>
public sealed class RequireRequestedWithAttributeTests
{
    /// <summary>Runs the filter over a request.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="configure">Adds headers to the request.</param>
    /// <returns>The result the filter set, or <see langword="null"/> when it let the request through.</returns>
    private static IActionResult? Run(string method, Action<IHeaderDictionary>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Method = method;
        configure?.Invoke(context.Request.Headers);
        var filter = new AuthorizationFilterContext(
            new ActionContext(context, new RouteData(), new ActionDescriptor()), []);

        new RequireRequestedWithAttribute().OnAuthorization(filter);

        return filter.Result;
    }

    /// <summary>A write with no header and no bearer token is refused 403 with the CSRF code, with no cookie involved.</summary>
    /// <param name="method">A method that changes state.</param>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void AWriteWithNeitherTheHeaderNorABearer_IsRefused(string method)
    {
        var result = Run(method);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        content.Content.Should().Contain(SharedErrorCodes.CsrfHeaderMissing);
    }

    /// <summary>The header, in any case of its value, lets a write through.</summary>
    [Fact]
    public void AWriteWithTheHeader_IsLetThrough()
    {
        Run("POST", headers => headers["X-Requested-With"] = "xmlhttprequest").Should().BeNull();
    }

    /// <summary>A bearer token lets a write through without the header.</summary>
    [Fact]
    public void AWriteWithABearerToken_IsLetThrough()
    {
        Run("POST", headers => headers.Authorization = $"{BearerScheme.HeaderPrefix}a-token").Should().BeNull();
    }

    /// <summary>An Authorization header that is not a bearer token exempts nothing.</summary>
    [Fact]
    public void AWriteWithABasicAuthorization_IsRefused()
    {
        Run("POST", headers => headers.Authorization = "Basic dXNlcjpwYXNz").Should().BeOfType<ContentResult>();
    }

    /// <summary>A read is never refused.</summary>
    /// <param name="method">A method that cannot change state.</param>
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void AReadWithNothing_IsLetThrough(string method)
    {
        Run(method).Should().BeNull();
    }

    /// <summary>The refusal sentence is localised: it is read through the validation messages the middleware uses.</summary>
    [Fact]
    public void TheRefusalSentence_IsTheSharedLocalisedOne()
    {
        var localizer = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<ValidationMessages>>();

        var content = (ContentResult)Run("POST")!;

        content.Content.Should().Contain(localizer["CsrfHeaderMissingDetail"].Value);
    }
}
