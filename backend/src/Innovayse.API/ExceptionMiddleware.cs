namespace Innovayse.API;

using System.Net;
using System.Text.Json;

/// <summary>
/// Global exception-handling middleware that maps well-known exceptions to HTTP status codes.
/// Prevents unhandled exceptions from propagating to the test host or client.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class ExceptionMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
            // Log the original message in non-production environments
            _ = ex;
        }
    }

    /// <summary>
    /// Writes a JSON error response with the specified status code and message.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="message">The error message to include in the response body.</param>
    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }
}
