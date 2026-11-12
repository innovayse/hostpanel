using Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.CreateTaxRule;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.DeleteTaxRule;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.UpdateTaxRule;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Common;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.GetTaxRule;
using Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin API for tax rules: the rate new invoices are taxed at, per country and per region of a country.</summary>
[Route("api/admin/tax-rules")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class TaxRulesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public TaxRulesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Every tax rule.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the rules; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaxRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<TaxRuleDto>>>(new ListTaxRulesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>A single tax rule.</summary>
    /// <param name="id">The rule's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the rule; 404 with <c>TAX_RULE_NOT_FOUND</c>; 400 when the route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaxRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TaxRuleDto>>(new GetTaxRuleQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a tax rule for a country, or for one region of it.</summary>
    /// <param name="command">The rule's name, country, region, rate and switch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new rule's id; 400 when a field is refused; 409 with <c>TAX_RULE_ALREADY_EXISTS</c> when the country and region have a rule; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTaxRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/tax-rules/{id}", id));
    }

    /// <summary>Replaces a tax rule's name, country, region, rate and switch.</summary>
    /// <param name="id">The rule's primary key; wins over any id in the body.</param>
    /// <param name="command">The rule's new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>TAX_RULE_NOT_FOUND</c>; 409 with <c>TAX_RULE_ALREADY_EXISTS</c> when another rule holds the country and region; 400 when a field is refused; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateTaxRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes a tax rule. Invoices already issued keep the rate they were taxed at.</summary>
    /// <param name="id">The rule's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success; 404 with <c>TAX_RULE_NOT_FOUND</c>; 400 when the route fails validation; 401 without a session; 403 for a caller who is not an admin; 429 when the rate limit is hit.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteTaxRuleCommand(id), ct);
        return HandleResult(outcome);
    }
}
