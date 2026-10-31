using System.Net;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models.Enums;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;

/// <summary>
/// Plesk implementation of <see cref="IProvisioningProvider"/>, created per server by
/// <see cref="IProvisioningProviderFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One Plesk customer and one subscription per service.</b> The service's username is the
/// customer's login, its system user and its provisioning reference; the product's package names
/// the service plan. Suspending, unsuspending and terminating act on the customer, which carries
/// its subscription with it.
/// </para>
/// <para>
/// <b>A refusal is not always a failure.</b> A retry after a lost answer meets the customer or the
/// subscription the first attempt already made, and a suspend, unsuspend or terminate can find the
/// account already as asked. After a refusal the provider asks Plesk what the account looks like
/// and treats "already as asked" as success. Plesk's "already exists" code is not trusted on its
/// own (it is also used for other refusals), so the lookup decides every time.
/// </para>
/// </remarks>
public sealed class PleskProvisioningProvider : IProvisioningProvider
{
    /// <summary>The failure reason handed back when an account cannot be created; the panel's own text is never repeated.</summary>
    private const string CreateFailedReason = "The control panel refused to create the account.";

    /// <summary>The failure reason handed back when the server has no address to host the domain on.</summary>
    private const string NoIpReason = "The control panel has no shared IP address to host the account on.";

    /// <summary>The panel page that enters a session opened over the API, followed by its id.</summary>
    private const string SessionEntryPath = "/enterprise/rsession_init.php?PLESKSESSID=";

    /// <summary>The Plesk XML API client.</summary>
    private readonly PleskApiClient _client;

    /// <summary>Where and how to reach this Plesk server.</summary>
    private readonly PleskConnection _connection;

    /// <summary>The server address subscriptions are hosted on, or null to use the first shared one Plesk lists.</summary>
    private readonly string? _serverIp;

    /// <summary>Records each operation by service id or account reference, never by domain or panel text.</summary>
    private readonly ILogger<PleskProvisioningProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="PleskProvisioningProvider"/> class.</summary>
    /// <param name="connection">Where and how to reach this Plesk server.</param>
    /// <param name="serverIp">The server address subscriptions are hosted on, or null to use the first shared one Plesk lists.</param>
    /// <param name="client">The Plesk XML API client.</param>
    /// <param name="logger">Records each operation by service id or account reference, never by domain or panel text.</param>
    public PleskProvisioningProvider(PleskConnection connection, string? serverIp, PleskApiClient client, ILogger<PleskProvisioningProvider> logger)
    {
        _connection = connection;
        _serverIp = serverIp;
        _client = client;
        _logger = logger;
    }

    /// <summary>Creates the customer and its subscription for a service.</summary>
    /// <param name="req">The service's domain, username, password and package (the Plesk service plan name).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Created with the username as the reference; AlreadyExists when a customer with the username
    /// is already there, with its subscription's domain (or none) for the caller to judge; Refused
    /// otherwise.
    /// </returns>
    /// <remarks>
    /// <para>
    /// An existing customer is never built on. Plesk cannot say who made it, so a customer that
    /// already holds the username is reported, with the domain of its subscription, and the caller
    /// adopts it only when that domain is the service's own. A customer with no subscription is
    /// reported with no domain, which the caller treats as a conflict for staff: adding this
    /// service's subscription to a stranger's customer would hand them the client's site, and a
    /// later terminate would delete the stranger's account.
    /// </para>
    /// <para>
    /// The customer this call made is removed again only when Plesk positively refused the
    /// subscription and positively holds none. A lost answer is not a refusal: the subscription may
    /// well exist, so it is left for the retry to find.
    /// </para>
    /// </remarks>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var ip = await _client.FindHostingIpAsync(_connection, _serverIp, ct);
        if (string.IsNullOrWhiteSpace(ip))
        {
            _logger.LogWarning("Plesk provisioning for service {ServiceId} found no shared IP address.", req.ServiceId);
            return ProvisioningResult.Refused(NoIpReason);
        }

        if (!(await _client.AddCustomerAsync(_connection, req.Username, req.Password, ct)).IsSuccess)
        {
            return await ReportExistingCustomerAsync(req, ct);
        }

        var subscription = new PleskNewSubscription(req.Username, req.DomainName, ip, req.Package, req.Username, req.Password);
        var added = await _client.AddSubscriptionAsync(_connection, subscription, ct);
        if (added.IsSuccess)
        {
            _logger.LogInformation("Plesk account created for service {ServiceId}.", req.ServiceId);
            return ProvisioningResult.Created(req.Username);
        }

        var lookup = await _client.FindSubscriptionAsync(_connection, req.Username, ct);
        if (lookup.Value is { } made)
        {
            _logger.LogInformation("Plesk subscription for service {ServiceId} was made despite the lost answer.", req.ServiceId);
            return ProvisioningResult.AlreadyExists(req.Username, made.Domain);
        }

        if (added.ErrorCode is not null && lookup.Outcome is PleskLookupOutcome.NotFound)
        {
            // A refused order (a prohibited domain, an unknown plan) leaves nothing behind on the
            // panel and holds no licence slot.
            await _client.DeleteCustomerAsync(_connection, req.Username, ct);
        }

        _logger.LogWarning("Plesk refused to create the subscription for service {ServiceId}.", req.ServiceId);
        return ProvisioningResult.Refused(CreateFailedReason);
    }

    /// <summary>Reports a customer that already holds the username, without touching it.</summary>
    /// <param name="req">The order whose customer could not be created.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// AlreadyExists with its subscription's domain, or with none when it has no subscription;
    /// Refused when no such customer exists or its state cannot be read.
    /// </returns>
    private async Task<ProvisioningResult> ReportExistingCustomerAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        var customer = await _client.FindCustomerAsync(_connection, req.Username, ct);
        if (customer.Outcome is not PleskLookupOutcome.Found)
        {
            _logger.LogWarning("Plesk refused to create the customer for service {ServiceId}.", req.ServiceId);
            return ProvisioningResult.Refused(CreateFailedReason);
        }

        var subscription = await _client.FindSubscriptionAsync(_connection, req.Username, ct);
        if (subscription.Outcome is PleskLookupOutcome.Unknown)
        {
            _logger.LogWarning("Plesk customer for service {ServiceId} exists but its subscription could not be read.", req.ServiceId);
            return ProvisioningResult.Refused(CreateFailedReason);
        }

        _logger.LogInformation("Plesk customer for service {ServiceId} already exists.", req.ServiceId);
        return ProvisioningResult.AlreadyExists(req.Username, subscription.Value?.Domain);
    }

    /// <summary>Disables the service's customer, and with it the subscription.</summary>
    /// <param name="req">The service's provisioning reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when Plesk refuses and the customer is not already disabled.</exception>
    public async Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        await SetSuspendedAsync(req.ProvisioningRef, true, ct);
    }

    /// <summary>Re-enables the service's customer, and with it the subscription.</summary>
    /// <param name="provisioningRef">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when Plesk refuses and the customer is not already active.</exception>
    public Task UnsuspendAsync(string provisioningRef, CancellationToken ct) =>
        SetSuspendedAsync(provisioningRef, false, ct);

    /// <summary>Removes the service's customer together with its subscription.</summary>
    /// <param name="req">The service's provisioning reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when Plesk refuses and does not positively report the customer gone.</exception>
    public async Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if ((await _client.DeleteCustomerAsync(_connection, req.ProvisioningRef, ct)).IsSuccess)
        {
            _logger.LogInformation("Plesk account {User} terminated.", req.ProvisioningRef);
            return;
        }

        var lookup = await _client.FindCustomerAsync(_connection, req.ProvisioningRef, ct);
        if (lookup.Outcome is not PleskLookupOutcome.NotFound)
        {
            throw new InvalidOperationException("Plesk terminate failed.");
        }

        _logger.LogInformation("Plesk account {User} was already removed.", req.ProvisioningRef);
    }

    /// <summary>Returns the sign-in details of the service's account.</summary>
    /// <param name="provisioningRef">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The login, the subscription's domain, the server and the panel address; Plesk never reveals a password.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the account's subscription cannot be found.</exception>
    public async Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        // Plesk is the source of truth for the domain; a missing subscription is a failed read,
        // never a made-up domain.
        var subscription = await FindSubscriptionAsync(provisioningRef, ct)
            ?? throw new InvalidOperationException("The hosting account was not found on the server.");

        return new ServiceCredentials(
            provisioningRef,
            null,
            subscription.Domain,
            _serverIp ?? string.Empty,
            _connection.BaseUrl);
    }

    /// <summary>Opens a Plesk session for the service's customer and returns the link that enters it.</summary>
    /// <param name="provisioningRef">The customer's login.</param>
    /// <param name="callerIp">The address of the browser that will open the link; Plesk opens the session from this address only.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The panel's session entry link, e.g. <c>https://plesk.example.com:8443/enterprise/rsession_init.php?PLESKSESSID=…</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the caller's address is unknown, loopback or unspecified, or Plesk refuses the session.
    /// </exception>
    /// <remarks>
    /// Plesk's <c>server.create_session</c> binds the session to the browser's address and refuses it
    /// from any other, so a link issued without that address could not work and is never issued.
    /// </remarks>
    public async Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        if (!IPAddress.TryParse(callerIp, out var address))
        {
            throw new InvalidOperationException("Plesk single sign-on needs the browser's address, and none is known.");
        }

        // The address as the browser's own connection to Plesk will show it: an IPv4 browser behind a
        // dual-stack listener arrives as ::ffff:a.b.c.d, which Plesk would never see.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        switch (OutboundAddressPolicy.Classify(address))
        {
            case OutboundAddressClass.Refused:
                // Loopback or unspecified is never a browser: the trusted-proxy step did not resolve
                // the visitor, and a session bound to it could not be opened.
                _logger.LogWarning("Plesk SSO for {User} refused: the caller's address is loopback or unspecified; check the trusted proxies.", provisioningRef);
                throw new InvalidOperationException("The caller's address is not a browser's address.");
            case OutboundAddressClass.Private:
                // Legitimate on a private network, but also what a proxy that was not trusted looks like.
                _logger.LogWarning("Plesk SSO for {User} binds the session to a private address; if the link fails, check the trusted proxies.", provisioningRef);
                break;
        }

        var sessionId = await _client.CreateSessionAsync(_connection, provisioningRef, address.ToString(), ct)
            ?? throw new InvalidOperationException("Plesk refused to open a session.");

        _logger.LogInformation("Plesk session opened for {User}.", provisioningRef);
        return $"{_connection.BaseUrl.TrimEnd('/')}{SessionEntryPath}{Uri.EscapeDataString(sessionId)}";
    }

    /// <summary>Changes the service's password on the panel and for its system (FTP/SSH) user.</summary>
    /// <param name="provisioningRef">The customer's login.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when Plesk refuses either change.</exception>
    /// <remarks>
    /// The account is created with one password for both, so both change together; otherwise FTP and
    /// SSH would keep a password the client can no longer see anywhere.
    /// </remarks>
    public async Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        if (!(await _client.SetCustomerPasswordAsync(_connection, provisioningRef, newPassword, ct)).IsSuccess)
        {
            throw new InvalidOperationException("Plesk password change failed.");
        }

        if (!(await _client.SetSystemUserPasswordAsync(_connection, provisioningRef, newPassword, ct)).IsSuccess)
        {
            throw new InvalidOperationException("Plesk system user password change failed.");
        }

        _logger.LogInformation("Plesk password changed for {User}.", provisioningRef);
    }

    /// <summary>Moves the service's subscription onto another service plan.</summary>
    /// <param name="provisioningRef">The customer's login.</param>
    /// <param name="newPackage">The name of the service plan to switch to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the plan does not exist or Plesk refuses the switch.</exception>
    public async Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        var plan = await _client.FindPlanGuidAsync(_connection, newPackage, ct);
        if (plan.Value is not { } guid)
        {
            throw new InvalidOperationException("The Plesk service plan was not found.");
        }

        if (!(await _client.SwitchSubscriptionPlanAsync(_connection, provisioningRef, guid, ct)).IsSuccess)
        {
            throw new InvalidOperationException("Plesk package change failed.");
        }

        _logger.LogInformation("Plesk package changed for {User}.", provisioningRef);
    }

    /// <summary>Disables or re-enables a customer, accepting one that is already as asked.</summary>
    /// <param name="login">The customer's login.</param>
    /// <param name="suspended">Whether the customer is to be disabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the customer is as asked.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Plesk refuses and the customer is not already as asked.</exception>
    private async Task SetSuspendedAsync(string login, bool suspended, CancellationToken ct)
    {
        if ((await _client.SetCustomerSuspendedAsync(_connection, login, suspended, ct)).IsSuccess)
        {
            _logger.LogInformation("Plesk account {User} suspended: {Suspended}.", login, suspended);
            return;
        }

        var lookup = await _client.FindCustomerAsync(_connection, login, ct);
        if (lookup.Value?.IsSuspended != suspended)
        {
            throw new InvalidOperationException(suspended ? "Plesk suspend failed." : "Plesk unsuspend failed.");
        }

        _logger.LogInformation("Plesk account {User} was already suspended: {Suspended}.", login, suspended);
    }

    /// <summary>Looks up the customer's subscription, treating anything but "found" as none.</summary>
    /// <param name="login">The customer's login.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The subscription, or null.</returns>
    private async Task<PleskSubscription?> FindSubscriptionAsync(string login, CancellationToken ct) =>
        (await _client.FindSubscriptionAsync(_connection, login, ct)).Value;
}
