using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;

namespace Innovayse.Hostpanel.Shared.Authorization;

/// <summary>
/// Answers an authorization refusal with the product's own <see cref="ErrorResponse"/> body, and leaves every other
/// outcome to the framework's default <see cref="IAuthorizationMiddlewareResultHandler"/>.
/// </summary>
/// <remarks>
/// The refusal is written here and the default handler is not asked to handle it. Its handling of a refusal is to
/// challenge or forbid <b>every</b> scheme the policy names, one after another; this product's policies name three
/// (sign-on bearer, local bearer, session cookie). The first scheme to answer writes the body and starts the
/// response, and the next one then sets a status on a response that has already started, which throws
/// <see cref="InvalidOperationException"/> and ends in the exception middleware as a logged
/// <c>BUSINESS_RULE_VIOLATION</c> for what was an ordinary 401 or 403.
/// </remarks>
public sealed class ProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    /// <summary>
    /// The stock handler every outcome other than a refusal is delegated to. Stateless and safe to share: it holds
    /// no per-request data.
    /// </summary>
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Forbidden && !authorizeResult.Challenged)
        {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        // A flushed response cannot be modified and trying throws, so a response flushed for an unrelated
        // reason (streaming, a websocket upgrade) is left as it is.
        if (context.Response.HasStarted)
        {
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                SharedErrorCodes.Forbidden,
                "Forbidden",
                "ForbiddenDetail");
        }
        else if (authorizeResult.Challenged)
        {
            await WriteAsync(
                context,
                StatusCodes.Status401Unauthorized,
                SharedErrorCodes.Unauthorized,
                "Unauthorized",
                "UnauthorizedDetail");
        }
    }

    /// <summary>Writes the localized <see cref="ErrorResponse"/> that answers the refusal.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="status">401 or 403.</param>
    /// <param name="code">The registry code the status is answered with.</param>
    /// <param name="titleKey">The resource key for the title.</param>
    /// <param name="detailKey">The resource key for the detail.</param>
    /// <returns>A task that completes when the response is written.</returns>
    private static async Task WriteAsync(
        HttpContext context, int status, string code, string titleKey, string detailKey)
    {
        // A scheme whose default outcome is a redirect (the cookie scheme's access-denied page)
        // sets Location without writing a body, so HasStarted is still false here and this would
        // otherwise ship a stray header alongside the JSON body below.
        context.Response.Headers.Remove(HeaderNames.Location);

        var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<ValidationMessages>>();
        var body = new ErrorResponse(code, localizer[titleKey], status, localizer[detailKey]);

        await ErrorBody.WriteAsync(context, body, context.RequestAborted);
    }
}
