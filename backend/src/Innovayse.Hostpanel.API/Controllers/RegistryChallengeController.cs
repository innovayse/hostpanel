using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryChallenge;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The address the plugin registry fetches to confirm this installation: <c>/.well-known/hostpanel-registry/{id}</c> answers
/// exactly the challenge the registry gave this installation for that instance id. Anonymous by design, because the registry has
/// no credential for this installation yet and the challenge only proves control of the address.
/// </summary>
[AllowAnonymous]
[Route(".well-known/hostpanel-registry")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class RegistryChallengeController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public RegistryChallengeController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Answers the challenge stored for an instance id.</summary>
    /// <param name="instanceId">The instance id the registry gave this installation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the challenge as <c>text/plain</c> and <c>Cache-Control: no-store</c>; 404 for any other id; 429 when the rate limit is hit.</returns>
    [HttpGet("{instanceId:guid}")]
    [Produces("text/plain", "application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid instanceId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<string>>(new GetRegistryChallengeQuery(instanceId), ct);
        return HandlePlainText(outcome);
    }
}
