using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Provisioning.Commands.RefreshServiceFromPanel;
using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Commands.ProvisionService;

/// <summary>
/// Provisions a pending hosting service: selects a server, creates the account on its panel with
/// the service's stored hosting details, and activates the service on that server. The one place
/// an account is created, for the staff route and the client setup wizard alike.
/// </summary>
/// <remarks>
/// Safe to retry. The password is saved before the panel is called, so a retry sends the same
/// credentials the panel may already hold; a retry that meets the account an earlier attempt
/// created adopts it when it is this service's (same username and domain) and refuses with
/// <c>PROVISIONING_ACCOUNT_CONFLICT</c> otherwise, instead of failing the same way forever. The
/// billing period starts at the activation moment, not the order date.
/// </remarks>
public sealed class ProvisionServiceHandler
{
    /// <summary>Length, in bytes, of the random password generated for a new hosting account.</summary>
    private const int GeneratedPasswordByteLength = 12;

    /// <summary>Length, in characters, the generated password is truncated to.</summary>
    private const int GeneratedPasswordLength = 16;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the service's product is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the provisioning provider configured for the selected server.</summary>
    private readonly IProvisioningProviderFactory _providerFactory;

    /// <summary>Selects the optimal server using the proportional-fill strategy.</summary>
    private readonly IServerSelector _serverSelector;

    /// <summary>Logs a provider failure without putting its detail on the wire.</summary>
    private readonly ILogger<ProvisionServiceHandler> _logger;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the service's product is read through.</param>
    /// <param name="providerFactory">Creates the provisioning provider configured for the selected server.</param>
    /// <param name="serverSelector">Selects the optimal server using the proportional-fill strategy.</param>
    /// <param name="logger">Logs a provider failure without putting its detail on the wire.</param>
    /// <param name="clock">Reads the current moment.</param>
    public ProvisionServiceHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IProvisioningProviderFactory providerFactory,
        IServerSelector serverSelector,
        ILogger<ProvisionServiceHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _providerFactory = providerFactory;
        _serverSelector = serverSelector;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="cmd">The service to provision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_PENDING</c>
    /// when the service is not Pending, <c>SERVICE_HOSTING_DETAILS_MISSING</c> when it has no
    /// domain or username yet, <c>PRODUCT_PACKAGE_MISSING</c> when its product names no hosting
    /// package, <c>PRODUCT_SERVER_GROUP_REQUIRED</c> when the product has no server group and servers
    /// run more than one supported panel, <c>NO_ELIGIBLE_SERVER</c> when no server has room, <c>CPANEL_USERNAME_INVALID</c>
    /// or <c>CPANEL_DOMAIN_INVALID</c> when the panel would never accept the stored username or domain, <c>PROVISIONING_ACCOUNT_CONFLICT</c>
    /// when the panel holds an account under the username that is not this service's, or
    /// <c>PROVISIONING_UPSTREAM_FAILED</c> when the provider rejects the request or answers a
    /// success without an account reference, or <c>PROVISIONING_OUTCOME_UNKNOWN</c> when the
    /// provider could not tell whether the account was created and the call is to be retried, <c>PROVISIONING_PENDING</c>
    /// when the panel accepted the account and is still creating it, or the panel's own refusal code
    /// (<see cref="PanelErrorCodes"/>) when a panel plugin refused with a reason.
    /// </returns>
    public async Task<Result> HandleAsync(ProvisionServiceCommand cmd, CancellationToken ct)
    {
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.Status is not ServiceStatus.Pending)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_PENDING);
        }

        // A real panel account is created from these, so a made-up name is never sent in their place.
        if (string.IsNullOrWhiteSpace(service.Domain) || string.IsNullOrWhiteSpace(service.Username))
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_HOSTING_DETAILS_MISSING);
        }

        // The product belongs to the Products module, so its package and server group are asked through its query.
        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([service.ProductId]), ct);
        var product = products.Value?.FirstOrDefault();
        if (product is null || string.IsNullOrWhiteSpace(product.PackageName))
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_PACKAGE_MISSING);
        }

        // Only a server whose panel the factory can drive is ever chosen, in a group or not: any
        // other would only fail the order at the panel.
        var supported = await _providerFactory.SupportedModulesAsync(ct);
        ServerConnectionDto? server;
        if (product.ServerGroupId is { } groupId)
        {
            server = await _serverSelector.SelectFromGroupAsync(groupId, supported, ct);
        }
        else
        {
            // A package names a plan on one panel, so with no group the panel must be unambiguous:
            // servers on one supported panel decide it; servers on several leave it to staff.
            var inUse = await _serverSelector.ModulesInUseAsync(supported, ct);
            if (inUse.Count > 1)
            {
                return Result.FailureCode(HostpanelErrorCodes.PRODUCT_SERVER_GROUP_REQUIRED);
            }

            server = await _serverSelector.SelectAsync(inUse, ct);
        }

        if (server is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_ELIGIBLE_SERVER);
        }

        // A staff-provisioned service may have no password yet; the generated one is stored with
        // the other details so the account's credentials are never known to the panel alone.
        var password = string.IsNullOrEmpty(service.Password) ? GeneratePassword() : service.Password;
        service.AssignHostingAccount(service.Domain, service.Username, password);

        // Saved before the panel is called: if the answer is lost after the panel created the
        // account, the retry must send the password the account was created with.
        await _context.SaveChangesAsync(ct);

        // The owner contact a panel keeps comes from the Clients module, through its brief query.
        var client = await _bus.GetRequiredClientBriefAsync(service.ClientId, withEmail: true, ct);
        var order = new ProvisionAccountOrder(
            service.Id, service.Domain, service.Username, password, product.PackageName,
            client.Id, client.Email, $"{client.FirstName} {client.LastName}".Trim());

        // The username and domain codes are the provider stopping the call before the panel was asked (staff can fix the stored
        // value); PROVISIONING_OUTCOME_UNKNOWN is a create that may or may not have happened, which is not a refusal: the service
        // stays Pending with its saved password and the retry adopts the account if the first attempt made it.
        var call = await PanelCall.RunAsync(
            () => _providerFactory.CreateFor(server).ProvisionAsync(order, ct),
            _logger, "Provisioning", service.Id, server.Id, HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED,
            HostpanelErrorCodes.CPANEL_USERNAME_INVALID, HostpanelErrorCodes.CPANEL_DOMAIN_INVALID, HostpanelErrorCodes.PROVISIONING_OUTCOME_UNKNOWN);
        if (call.IsFailure)
        {
            return Result.FailureCode(call.ErrorCode!);
        }

        var result = call.Value!;
        if (result.Outcome is ProvisioningOutcome.Refused)
        {
            _logger.LogWarning(
                "Provisioning service {ServiceId} on server {ServerId} was refused: {ErrorMessage}",
                service.Id, server.Id, result.ErrorMessage);
            return Result.FailureCode(HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED);
        }

        if (result.Outcome is ProvisioningOutcome.Pending)
        {
            // The panel is still creating the account. Nothing is activated: the service keeps its saved details and stays
            // Pending, and the retry finds the account (the panel answers AlreadyApplied once it is ready) and adopts it.
            _logger.LogInformation(
                "Provisioning service {ServiceId} on server {ServerId} is pending on the panel.", service.Id, server.Id);
            return Result.FailureCode(HostpanelErrorCodes.PROVISIONING_PENDING);
        }

        if (result.Outcome is ProvisioningOutcome.AlreadyExists
            && !string.Equals(result.ExistingAccountDomain, service.Domain, StringComparison.OrdinalIgnoreCase))
        {
            // Somebody else's account holds the username: adopting it would hand this client a
            // stranger's hosting, so the service stays Pending for staff to resolve.
            _logger.LogError(
                "Provisioning service {ServiceId} on server {ServerId} met an existing account that is not the service's; staff attention needed.",
                service.Id, server.Id);
            return Result.FailureCode(HostpanelErrorCodes.PROVISIONING_ACCOUNT_CONFLICT);
        }

        if (string.IsNullOrWhiteSpace(result.ProvisioningRef))
        {
            // Without a reference no later suspend, unsuspend or terminate could find the account.
            _logger.LogWarning(
                "Provisioning service {ServiceId} on server {ServerId} answered {Outcome} without an account reference.",
                service.Id, server.Id, result.Outcome);
            return Result.FailureCode(HostpanelErrorCodes.PROVISIONING_UPSTREAM_FAILED);
        }

        // Record the server alongside the activation: the suspend, unsuspend, terminate and
        // single-sign-on handlers all give up when ServerId is null, so a service activated
        // without one could never be acted on again. The billing period is anchored on the
        // activation moment, so the client pays from when the account exists.
        var now = _clock.UtcNow;
        service.ActivateOn(server.Id, result.ProvisioningRef, now, now);

        await _context.SaveChangesAsync(ct);

        await RefreshAssignedIpAsync(service.Id, ct);
        return Result.Success();
    }

    /// <summary>
    /// Reads the account just created back from its panel so the service shows the IP address it is served on. The service is already
    /// active and saved, so a panel that cannot say (a provider that cannot read one account, a refusal, an outage) is logged and
    /// leaves the provisioning a success: the address can be refreshed later from the service.
    /// </summary>
    /// <param name="serviceId">The service that was just activated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the panel was asked.</returns>
    private async Task RefreshAssignedIpAsync(int serviceId, CancellationToken ct)
    {
        try
        {
            var refreshed = await _bus.InvokeAsync<Result>(new RefreshServiceFromPanelCommand(serviceId), ct);
            if (refreshed.IsFailure)
            {
                _logger.LogInformation(
                    "Service {ServiceId} was provisioned but its account could not be read back from the panel: {ErrorCode}.",
                    serviceId, refreshed.ErrorCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing the read-back does may undo a provisioning that already succeeded and was saved.
            _logger.LogWarning(ex, "Service {ServiceId} was provisioned but reading its account back from the panel failed.", serviceId);
        }
    }

    /// <summary>Generates a cryptographically random password for the new hosting account.</summary>
    /// <returns>A 16-character password string.</returns>
    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(GeneratedPasswordByteLength);
        return Convert.ToBase64String(bytes).Replace("=", string.Empty, StringComparison.Ordinal)[..GeneratedPasswordLength];
    }
}
