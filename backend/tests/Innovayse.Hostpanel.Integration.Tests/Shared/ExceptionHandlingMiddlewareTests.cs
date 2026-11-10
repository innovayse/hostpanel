using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Middleware;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using ApplicationErrorMessages = Innovayse.Hostpanel.Application.Resources.ErrorMessages;
using SharedValidationMessages = Innovayse.Hostpanel.Shared.Resources.ValidationMessages;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="ExceptionHandlingMiddleware"/>'s catch-all, against the middleware directly rather
/// than through a live route: a 500 is answered only for a fault nothing upstream classified, and
/// this suite does not ship a route whose only job is to throw one on demand.
/// </summary>
public sealed class ExceptionHandlingMiddlewareTests
{
    /// <summary>Builds the localizer the middleware resolves its sentences from, without a web host.</summary>
    /// <returns>The localizer over <see cref="SharedValidationMessages"/>.</returns>
    private static IStringLocalizer<SharedValidationMessages> BuildLocalizer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedValidationMessages>>();
    }

    /// <summary>
    /// Builds the resolver a <see cref="DomainException"/> is mapped through, without a web host:
    /// the same <see cref="ErrorMessageResolver"/> the API registers, over the real
    /// <c>ErrorMessages.resx</c> set.
    /// </summary>
    /// <returns>A working <see cref="IErrorMessageResolver"/>.</returns>
    private static IErrorMessageResolver BuildErrorMessageResolver()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLocalization();
        var localizer = services.BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<ApplicationErrorMessages>>();
        return new ErrorMessageResolver(localizer);
    }

    /// <summary>Builds a bare <see cref="HttpContext"/> with an in-memory response body and no registered options.</summary>
    /// <returns>The context.</returns>
    private static DefaultHttpContext BuildContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// A fault nothing upstream classified answers 500, as <c>application/problem+json</c>, with a
    /// localized <c>detail</c>, and the compatibility shim carries the same pair by default.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnclassifiedFault_Answers500WithProblemJsonAndShimAsync()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidCastException("Something nothing upstream expected."),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            BuildLocalizer());

        var context = BuildContext();
        await middleware.InvokeAsync(context, BuildErrorMessageResolver());

        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        using var body = JsonDocument.Parse(await reader.ReadToEndAsync());

        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.InternalError);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.InternalError);
    }

    /// <summary>
    /// <see cref="ConcurrencyConflictException"/>, <c>IHostpanelDbContext.SaveChangesAsync</c>'s
    /// translation of EF's own optimistic-concurrency exception, answers 409, not the 500 an
    /// unclassified fault would, and not the 400 a handler's own <c>InvalidOperationException</c>
    /// arm would: the request was well-formed and nothing is broken, but the row the caller read
    /// no longer matches what is stored.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConcurrencyConflict_Answers409WithProblemJsonAndShimAsync()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ConcurrencyConflictException("Another writer modified the row concurrently."),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            BuildLocalizer());

        var context = BuildContext();
        await middleware.InvokeAsync(context, BuildErrorMessageResolver());

        context.Response.StatusCode.Should().Be(409);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        using var body = JsonDocument.Parse(await reader.ReadToEndAsync());

        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.Conflict);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.Conflict);
    }

    /// <summary>A client cancellation is not answered at all: there is no one left to write a body to.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientCancellation_WritesNothingAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var middleware = new ExceptionHandlingMiddleware(
            context =>
            {
                context.RequestAborted.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            BuildLocalizer());

        var context = BuildContext();
        context.RequestAborted = cts.Token;

        await middleware.InvokeAsync(context, BuildErrorMessageResolver());

        context.Response.Body.Length.Should().Be(0);
    }

    /// <summary>
    /// A <see cref="DomainException"/> thrown straight out of an aggregate (uncaught by the handler
    /// that called it) is mapped the same way a handler's own <c>Result</c> refusal is: the status
    /// and title come from <c>StatusMapping</c> by the exception's own code, and <c>detail</c> is
    /// resolved by that code from <c>ErrorMessages.resx</c> in the request's culture, never the
    /// exception's English message, which only reaches the log.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainException_MapsByCodeWithLocalizedDetailAsync()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new DomainRuleException(
                BillingErrors.InvoiceNotDraft, "Only Draft invoices can be published; current status is Unpaid."),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            BuildLocalizer());

        var context = BuildContext();
        var previousCulture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("ru");
        try
        {
            await middleware.InvokeAsync(context, BuildErrorMessageResolver());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previousCulture;
        }

        context.Response.StatusCode.Should().Be(400);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        using var body = JsonDocument.Parse(await reader.ReadToEndAsync());

        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVOICE_NOT_DRAFT);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        // The Russian sentence, not the exception's own English message.
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVOICE_NOT_DRAFT);
    }
}
