using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerConnection;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;

/// <summary>
/// Handles <see cref="AddExistingServiceCommand"/>: records an Active service for an account the
/// panel already holds, through <see cref="ClientService.Import"/>, so no welcome mail is sent and
/// nothing is provisioned.
/// </summary>
public sealed class AddExistingServiceHandler
{
    /// <summary>Database context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the queries of other modules.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Clock.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="AddExistingServiceHandler"/> class.</summary>
    /// <param name="context">Database context.</param>
    /// <param name="bus">Message bus.</param>
    /// <param name="clock">Clock.</param>
    public AddExistingServiceHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>Adds the service.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new service's id; <c>CLIENT_NOT_FOUND</c>, <c>PRODUCT_NOT_FOUND</c> or <c>SERVER_NOT_FOUND</c>;
    /// <c>SERVICE_HOSTING_ACCOUNT_EXISTS</c> when a service that is not terminated already holds that
    /// username on that server.
    /// </returns>
    public async Task<Result<int>> HandleAsync(AddExistingServiceCommand cmd, CancellationToken ct)
    {
        if (!await _bus.ClientExistsAsync(cmd.ClientId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([cmd.ProductId]), ct);
        if (products.Value?.FirstOrDefault() is null)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        var server = await _bus.InvokeAsync<Result<ServerConnectionDto>>(new GetServerConnectionQuery(cmd.ServerId), ct);
        if (!server.IsSuccess)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        // One account, one live service: a second would suspend, bill and terminate the same account twice.
        var username = cmd.Username.Trim();
        var usernameLower = username.ToLower();
        var taken = await _context.ClientServices.AnyAsync(
            s => s.ServerId == cmd.ServerId
                && s.Status != ServiceStatus.Terminated
                && s.Username != null && s.Username.ToLower() == usernameLower,
            ct);
        if (taken)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_EXISTS);
        }

        var now = _clock.UtcNow;
        var startedAt = cmd.StartedAt ?? now;
        var domain = cmd.Domain.Trim();

        // The password stays on the panel: HostPanel never learnt it, and the client changes it there.
        var svc = ClientService.Import(new ServiceImport(
            ClientId: cmd.ClientId,
            ProductId: cmd.ProductId,
            BillingCycle: cmd.BillingCycle,
            Status: ServiceStatus.Active,
            RegisteredAt: startedAt,
            StatusChangedAt: now,
            SuspensionSource: SuspensionSource.Other,
            HostingAccount: new ServiceHostingAccount(
                ServerId: cmd.ServerId, Domain: domain, ProvisioningRef: username, DedicatedIp: null, Username: username, Password: null)));

        // Import anchors the renewal one cycle after the start; only the amounts are added here.
        svc.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: cmd.BillingCycle,
            Quantity: 1,
            FirstPaymentAmount: cmd.RecurringAmount,
            RecurringAmount: cmd.RecurringAmount,
            NextRenewalAt: svc.NextRenewalAt,
            PaymentMethod: null,
            PromotionCode: null,
            SubscriptionId: null));
        svc.UpdateAdminNotes($"Added by staff on {now:yyyy-MM-dd} for an account that already existed on the server; nothing was provisioned.");

        _context.ClientServices.Add(svc);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(svc.Id);
    }
}
