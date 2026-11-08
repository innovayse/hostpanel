using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;

/// <summary>
/// Handles <see cref="RenewOrderedDomainCommand"/>: finds the domain by name, refuses one the
/// paying client does not own, and renews it through <see cref="RenewDomainCommand"/>.
/// </summary>
/// <remarks>
/// The lookup is by name and not scoped by client, which is why the owner is checked here.
/// Fulfillment runs later than checkout, from a payment webhook, against whatever the order line
/// says, so a check that ran once when the renewal invoice was raised guarantees nothing about the
/// message replayed here.
/// </remarks>
public sealed class RenewOrderedDomainHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus, for the registrar renewal.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<RenewOrderedDomainHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus, for the registrar renewal.</param>
    /// <param name="logger">Structured logger.</param>
    public RenewOrderedDomainHandler(
        IHostpanelDbContext context, IMessageBus bus, ILogger<RenewOrderedDomainHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The paying client, the domain name and the period.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the registrar has extended the registration; <c>DOMAIN_NOT_FOUND</c> when no
    /// such domain exists or it belongs to another client (one code, so the answer never tells
    /// whose it is); or whatever <see cref="RenewDomainCommand"/> refuses with. A domain that the same order line already
    /// renewed answers Success without calling the registrar again.
    /// </returns>
    public async Task<Result> HandleAsync(RenewOrderedDomainCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Domain names are case-insensitive; the order line keeps whatever case the cart sent.
        var name = cmd.DomainName.ToLowerInvariant();
        var domain = await _context.Domains
            .AsNoTracking()
            .Where(d => d.Name.ToLower() == name)
            .Select(d => new { d.Id, d.ClientId, d.LastRenewalOrderItemId })
            .FirstOrDefaultAsync(ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.ClientId != cmd.ClientId)
        {
            // Logged without naming the owning client: this is a refusal, and the log line that
            // records it should not be the place somebody learns whose domain it was.
            _logger.LogError(
                "Client {ClientId} paid to renew domain {DomainId}, which belongs to another client. Refusing.",
                cmd.ClientId, domain.Id);
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (cmd.OrderItemId is not null && domain.LastRenewalOrderItemId == cmd.OrderItemId)
        {
            // This same order line already extended the domain and died before it recorded the line; extending it
            // again would charge the registrar for a second period the client paid for once.
            _logger.LogInformation(
                "Domain {DomainId} was already renewed for order line {OrderItemId}; the retry counts it as renewed.",
                domain.Id, cmd.OrderItemId);
            return Result.Success();
        }

        return await _bus.InvokeAsync<Result>(new RenewDomainCommand(domain.Id, cmd.Years, cmd.OrderItemId), ct);
    }
}
