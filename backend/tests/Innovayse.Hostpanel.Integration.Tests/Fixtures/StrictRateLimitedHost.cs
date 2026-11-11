using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A small host for the rate limit of a public controller over HTTP. The shared test host switches the limiter off so that suites
/// do not spend one budget between them, so a test of the limiter builds a host of its own: one real controller behind the real
/// limiter registration, running as a Development host, with the strict budget cut to a few requests.
/// </summary>
public static class StrictRateLimitedHost
{
    /// <summary>
    /// Builds and starts the host: only the given controller is mapped, the limiter is the platform's own and the bus is a Wolverine
    /// runtime with no handlers, so a request inside the budget reaches the action and fails there. The failure is answered as the
    /// 500 a real host answers, which is what "reached the action" looks like here; without it the test host would rethrow it.
    /// </summary>
    /// <typeparam name="TController">A controller of the API assembly, the only one mapped.</typeparam>
    /// <param name="permits">The requests the strict tier allows in the window.</param>
    /// <returns>The started host, to dispose when the test ends.</returns>
    public static async Task<WebApplication> StartAsync<TController>(int permits)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Host.UseWolverine();
        builder.Services.AddPlatformRateLimiting(builder.Environment);

        // A refusal is written with the sentence of its error code, which the limiter looks up through this resolver.
        builder.Services.AddPlatformLocalization();
        builder.Services.AddScoped<IErrorMessageResolver, ErrorMessageResolver>();
        builder.Services.AddSingleton(TimeProvider.System);

        // The controllers are anonymous, but an endpoint that carries authorization metadata needs the middleware to be present.
        builder.Services.AddAuthorization();
        builder.Services.Configure<RateLimitOptions>(options =>
            options.Strict = new RateLimitTierOptions { PermitLimit = permits, WindowSeconds = 60 });
        builder.Services.AddScoped<ActionTimingFilter>();
        builder.Services.AddOptions<ActionTimingOptions>();
        builder.Services.AddControllers().AddApplicationPart(typeof(TController).Assembly);

        var app = builder.Build();
        app.UseExceptionHandler(failed => failed.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }
}
