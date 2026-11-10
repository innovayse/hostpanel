using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.IntegrationEvents.Contracts;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Clients.Events;

/// <summary>
/// Wolverine handler that reacts to <see cref="ClientRegisteredIntegrationEvent"/>. Automatically
/// creates a <see cref="Client"/> record for every new registered user. The event arrives through a
/// durable queue, so it can be delivered more than once; a replay never creates a second client: a
/// subject that already has one is skipped, two deliveries racing each other are settled by the unique
/// index on <c>Clients.UserId</c> (the loser sees the winner's row and stops), and an account that no
/// longer exists (the demo reset truncated it) gets no client at all.
/// </summary>
public sealed class CreateClientOnRegisterHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Confirms the registered account still exists before a client row is linked to it.</summary>
    private readonly IUserService _users;

    /// <summary>Records a delivery that was skipped because its account is gone.</summary>
    private readonly ILogger<CreateClientOnRegisterHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">Reads the current moment.</param>
    /// <param name="users">Confirms the registered account still exists before a client row is linked to it.</param>
    /// <param name="logger">Records a delivery that was skipped because its account is gone.</param>
    public CreateClientOnRegisterHandler(
        IHostpanelDbContext context, IClock clock, IUserService users, ILogger<CreateClientOnRegisterHandler> logger)
    {
        _context = context;
        _clock = clock;
        _users = users;
        _logger = logger;
    }

    /// <summary>Creates a Client record when a new user registers.</summary>
    /// <param name="evt">The integration event published by <c>RegisterHandler</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DbUpdateException">The save failed for a reason other than a concurrent delivery creating the same client.</exception>
    public async Task HandleAsync(ClientRegisteredIntegrationEvent evt, CancellationToken ct)
    {
        if (await HasClientAsync(evt.UserId, ct))
        {
            return;
        }

        // Only RegisterHandler publishes this event, and only in local mode, where the account store is
        // this deployment's own; an account missing from it was removed after the event was queued (the
        // demo reset truncates every table), and a client row would point at nobody.
        if (await _users.FindByIdAsync(evt.UserId, ct) is null)
        {
            _logger.LogInformation("The registered account no longer exists; no client row is created for it.");
            return;
        }

        var client = Client.Create(
            evt.UserId, evt.FirstName, evt.LastName, evt.Email, _clock.UtcNow,
            registration: new RegistrationInfo(evt.IpAddress, evt.UserAgent, evt.DeviceType, evt.OperatingSystem, evt.Browser));

        // A client who registers has no say in groups: it joins the default one, when there is one.
        if (await _context.FindDefaultClientGroupIdAsync(ct) is { } defaultGroupId)
        {
            client.AssignToGroup(defaultGroupId);
        }

        _context.Clients.Add(client);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The failed save was rolled back, so the read below runs on a clean connection, and the
            // refused row is detached so nothing else in this scope tries to save it again.
            _context.Clients.Remove(client);
            if (!await HasClientAsync(evt.UserId, ct))
            {
                throw;
            }

            // A second delivery of the same event inserted the row between the check above and this
            // save, and the unique index refused this one. The account has its client, so this delivery
            // is done; throwing would make Wolverine retry the whole chain and repeat the other handlers
            // of the event (the welcome mail).
        }
    }

    /// <summary>Whether a client row is already linked to the subject.</summary>
    /// <param name="userId">The registered account's subject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a client row links to the subject.</returns>
    private Task<bool> HasClientAsync(string userId, CancellationToken ct)
    {
        return _context.Clients.AsNoTracking().AnyAsync(c => c.UserId == userId, ct);
    }
}
