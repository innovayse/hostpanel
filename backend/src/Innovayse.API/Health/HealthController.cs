namespace Innovayse.API.Health;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Liveness probe endpoint used by load balancers and integration tests
/// to verify the API process is running.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK when the API process is alive.</summary>
    /// <returns>A JSON object containing <c>status</c> and <c>timestamp</c>.</returns>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow });
}
