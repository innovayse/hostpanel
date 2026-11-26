using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerConnection;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Extensions;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.AddExistingService;

/// <summary>
/// Handles <see cref="AddExistingServiceCommand"/>: reads the account from the server's panel, and records an Active service for
/// it through <see cref="ClientService.Import"/> only when the panel holds it (or cannot say and the admin confirmed), so no
/// welcome mail is sent and nothing is provisioned.
/// </summary>
public sealed class AddExistingServiceHandler
{
    /// <summary>Database context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the queries of other modules.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Clock.</summary>
    private readonly IClock _clock;

    /// <summary>What the server's control panel accepts as an account username.</summary>
    private readonly IPanelAccountRules _panelRules;

    /// <summary>Creates the provisioning provider configured for the chosen server, which reads the account.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Logs a refusal or a failure of the panel without putting its detail on the wire.</summary>
    private readonly ILogger<AddExistingServiceHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="AddExistingServiceHandler"/> class.</summary>
    /// <param name="context">Database context.</param>
    /// <param name="bus">Message bus.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="panelRules">What the server's control panel accepts as an account username.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the chosen server, which reads the account.</param>
    /// <param name="logger">Logs a refusal or a failure of the panel without putting its detail on the wire.</param>
    public AddExistingServiceHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IClock clock,
        IPanelAccountRules panelRules,
        IProvisioningProviderFactory providerFactory,
        ILogger<AddExistingServiceHandler> logger)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _panelRules = panelRules;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>Adds the service.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new service's id; <c>CLIENT_NOT_FOUND</c>, <c>PRODUCT_NOT_FOUND</c> or <c>SERVER_NOT_FOUND</c>;
    /// <c>CPANEL_USERNAME_INVALID</c> when the server's panel cannot be addressed by that username;
    /// <c>SERVICE_HOSTING_ACCOUNT_EXISTS</c> when a service that is not terminated already holds that
    /// username on that server; <c>SERVICE_DOMAIN_ALREADY_SERVED</c> when another such service holds the domain and the admin has not
    /// confirmed it; <c>SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER</c> when the panel says it has no such account;
    /// <c>SERVICE_HOSTING_ACCOUNT_UNVERIFIED</c> when the panel cannot read accounts and the admin has not confirmed; or the panel's
    /// own refusal code or <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c> when the panel cannot be read.
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

        // The account already exists, so the looser rule applies: the name only has to be one the panel can be asked about.
        var username = cmd.Username.Trim();
        if (!_panelRules.AcceptsAccountUsername(server.Value!.Module, username))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CPANEL_USERNAME_INVALID);
        }

        // One account, one live service: a second would suspend, bill and terminate the same account twice.
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

        var domain = cmd.Domain.Trim();

        // The same domain on a second live service is nearly always a mistake (the account was meant for the first), so it
        // takes an explicit confirmation; it is not refused outright because one domain can carry two services.
        var domainLower = domain.ToLower();
        if (!cmd.ConfirmSharedDomain
            && await _context.ClientServices.AnyAsync(
                s => s.Status != ServiceStatus.Terminated && s.Domain != null && s.Domain.ToLower() == domainLower, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.SERVICE_DOMAIN_ALREADY_SERVED);
        }

        // Last, because it asks the panel: the account has to be there before a service is recorded for it.
        var verified = await VerifyAccountAsync(server.Value!, username, cmd.ConfirmUnverified, ct);
        if (verified.IsFailure)
        {
            return Result<int>.FailureCode(verified.ErrorCode!);
        }

        var now = _clock.UtcNow;
        var startedAt = cmd.StartedAt ?? now;

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
                ServerId: cmd.ServerId, Domain: domain, ProvisioningRef: username, DedicatedIp: verified.Value, Username: username, Password: null)));

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

    /// <summary>
    /// Asks the server's panel whether it holds the account. A panel that cannot read accounts leaves the admin to vouch for it
    /// (<paramref name="confirmUnverified"/>); one that says the account is missing is never overridden.
    /// </summary>
    /// <param name="server">The server whose panel is asked.</param>
    /// <param name="username">The account's username on the panel.</param>
    /// <param name="confirmUnverified">Whether the admin confirmed an account the panel cannot be asked about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the address the panel serves the account on (<see langword="null"/> when it says none, or when the account was
    /// confirmed unread); <c>SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER</c>; <c>SERVICE_HOSTING_ACCOUNT_UNVERIFIED</c>; or the panel's own
    /// refusal code or <c>SERVICE_PANEL_SYNC_UPSTREAM_FAILED</c>.
    /// </returns>
    private async Task<Result<string?>> VerifyAccountAsync(ServerConnectionDto server, string username, bool confirmUnverified, CancellationToken ct)
    {
        var read = await _providerFactory.ReadHostedAccountAsync(server, new AccountStateOrder(0, username), _logger, ct);
        if (read.IsSuccess)
        {
            return Result<string?>.Success(PanelIpAddress.Normalize(read.Value!.IpAddress));
        }

        return read.ErrorCode switch
        {
            HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND => Result<string?>.FailureCode(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_NOT_ON_SERVER),
            HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED when confirmUnverified => Result<string?>.Success(null),
            HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED => Result<string?>.FailureCode(HostpanelErrorCodes.SERVICE_HOSTING_ACCOUNT_UNVERIFIED),
            _ => Result<string?>.FailureCode(read.ErrorCode!),
        };
    }
}
