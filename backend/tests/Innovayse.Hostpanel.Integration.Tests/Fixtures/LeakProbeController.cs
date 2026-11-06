using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test-host-only controller that plays a misbehaving provider: each action sets every vendor
/// header it can think of on the response, as a careless proxy of an upstream answer would.
/// Registered only by <see cref="Innovayse.Hostpanel.Integration.Tests.Security.LeakGuardTests"/> through an application part; it does not exist
/// in the production host.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/leak-probe")]
public sealed class LeakProbeController : ControllerBase
{
    /// <summary>The vendor name the faulting action puts in its exception message.</summary>
    public const string VendorName = "ExampleRegistrar";

    /// <summary>Headers a provider could set that must never reach the client.</summary>
    public static readonly IReadOnlyDictionary<string, string> VendorHeaders = new Dictionary<string, string>
    {
        ["Server"] = "nginx/1.25 ExampleRegistrar",
        ["X-Powered-By"] = "Registrar-API",
        ["X-Provider-Name"] = "ExampleRegistrar",
        ["X-Provider-Request-Id"] = "ncp-123",
        ["X-Upstream"] = "api.registrar.example",
        ["X-Upstream-Status"] = "500",
        ["Via"] = "1.1 vendor-edge",
        ["X-AspNet-Version"] = "4.0.30319",
        ["X-Vendor-Trace"] = "abc",
    };

    /// <summary>
    /// Limit headers a provider could set. The guard allows them by name, but only on a 429: on any other response they
    /// were copied from somewhere else and must not arrive.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RateLimitHeaders = new Dictionary<string, string>
    {
        ["X-RateLimit-Limit"] = "5000",
        ["X-RateLimit-Remaining"] = "4999",
        ["X-RateLimit-Reset"] = "1893456000",
        ["X-RateLimit-Policy"] = "vendor-plan",
    };

    /// <summary>Headers that are ours and must survive the guard on a download.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedHeaders = new Dictionary<string, string>
    {
        ["Content-Disposition"] = "attachment; filename=\"export.csv\"",
        ["ETag"] = "\"v1\"",
    };

    /// <summary>Answers with a CSV body, the vendor headers and the allow-listed ones.</summary>
    /// <returns>A 200 result.</returns>
    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Download()
    {
        foreach (var (name, value) in VendorHeaders.Concat(RateLimitHeaders))
        {
            Response.Headers[name] = value;
        }
        foreach (var (name, value) in AllowedHeaders)
        {
            Response.Headers[name] = value;
        }

        return File(Encoding.UTF8.GetBytes("a,b\n1,2\n"), "text/csv");
    }

    /// <summary>Answers 429 with the vendor headers and the limit headers, as the limiter does with its own.</summary>
    /// <returns>A 429 result.</returns>
    [HttpGet("refused")]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Refused()
    {
        foreach (var (name, value) in VendorHeaders.Concat(RateLimitHeaders))
        {
            Response.Headers[name] = value;
        }

        return StatusCode(StatusCodes.Status429TooManyRequests);
    }

    /// <summary>Sets the vendor headers then faults with a message that names the vendor.</summary>
    /// <returns>Never returns.</returns>
    [HttpGet("fault")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Fault()
    {
        foreach (var (name, value) in VendorHeaders)
        {
            Response.Headers[name] = value;
        }
        // HttpRequestException, not InvalidOperationException: the edge answers the latter 400, and an upstream fault must be a 500.
        throw new HttpRequestException($"{VendorName} API Error 2019166: Domain not found at api.registrar.example");
    }
}
