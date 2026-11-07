using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

/// <summary>
/// cPanel implementation of <see cref="IProvisioningProvider"/> over WHM API 1, created per server
/// by <see cref="IProvisioningProviderFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One cPanel account per service.</b> The service's username is the account's username and its
/// provisioning reference; the product's package names the WHM package.
/// </para>
/// <para>
/// <b>A refusal is not always a failure.</b> A retry after a lost answer meets the account the
/// first attempt already made, and a suspend, unsuspend, terminate or package change can find the
/// account already as asked. After any answer that is not a success the provider looks the account
/// up and treats "already as asked" as success. A failed lookup is never read as "absent".
/// </para>
/// </remarks>
public sealed class WhmProvisioningProvider : IProvisioningProvider
{
    /// <summary>The failure reason handed back when an account cannot be created; WHM's own text is never repeated.</summary>
    private const string CreateFailedReason = "The control panel refused to create the account.";

    /// <summary>The WHM API client.</summary>
    private readonly WhmApiClient _client;

    /// <summary>Where and how to reach this WHM server.</summary>
    private readonly WhmConnection _connection;

    /// <summary>The server's address, shown with the account's sign-in details.</summary>
    private readonly string _serverIp;

    /// <summary>The cPanel address an account owner signs in at, e.g. <c>https://host:2083</c>.</summary>
    private readonly string _panelUrl;

    /// <summary>Records each operation by service id or account reference, never by domain or WHM text.</summary>
    private readonly ILogger<WhmProvisioningProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="WhmProvisioningProvider"/> class.</summary>
    /// <param name="connection">Where and how to reach this WHM server.</param>
    /// <param name="serverIp">The server's address, shown with the account's sign-in details.</param>
    /// <param name="panelUrl">The cPanel address an account owner signs in at, e.g. <c>https://host:2083</c>.</param>
    /// <param name="client">The WHM API client.</param>
    /// <param name="logger">Records each operation by service id or account reference, never by domain or WHM text.</param>
    public WhmProvisioningProvider(WhmConnection connection, string serverIp, string panelUrl, WhmApiClient client, ILogger<WhmProvisioningProvider> logger)
    {
        _connection = connection;
        _serverIp = serverIp;
        _panelUrl = panelUrl;
        _client = client;
        _logger = logger;
    }

    /// <summary>Creates the cPanel account for a service.</summary>
    /// <param name="req">The service's domain, username, password and package (the WHM package name).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Created with the username as the reference; AlreadyExists when an account with the username
    /// is already there, with its domain for the caller to judge; Refused when WHM answered and the
    /// account does not exist.
    /// </returns>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="HostpanelErrorCodes.CPANEL_USERNAME_INVALID"/> or
    /// <see cref="HostpanelErrorCodes.CPANEL_DOMAIN_INVALID"/>, before WHM is called, when the
    /// username or the domain is not one cPanel accepts.
    /// </exception>
    /// <exception cref="WhmUnavailableException">
    /// Thrown when the create could not be sent or its answer was lost and the follow up lookup
    /// could not tell whether the account exists, so a later retry can settle it.
    /// </exception>
    /// <remarks>
    /// An existing account is never built on. WHM cannot say who made it, so it is reported with
    /// its primary domain, and the caller adopts it only when that domain is the service's own.
    /// <c>createacct</c> makes the whole account or nothing, so a refusal leaves nothing to clean up.
    /// </remarks>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (!WhmInputRules.IsNewUsername(req.Username))
        {
            throw new DomainRuleException(HostpanelErrorCodes.CPANEL_USERNAME_INVALID, "The username is not a valid cPanel username.");
        }

        if (!WhmInputRules.IsDomain(req.DomainName))
        {
            throw new DomainRuleException(HostpanelErrorCodes.CPANEL_DOMAIN_INVALID, "The domain is not a valid domain name.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(req.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(req.Package);

        var created = await _client.CreateAccountAsync(_connection, req.Username, req.DomainName, req.Password, req.Package, ct);
        if (created.IsSuccess)
        {
            _logger.LogInformation("cPanel account created for service {ServiceId}.", req.ServiceId);
            return ProvisioningResult.Created(req.Username);
        }

        var lookup = await _client.FindAccountAsync(_connection, req.Username, ct);
        if (lookup.Value is { } existing)
        {
            _logger.LogInformation("cPanel account for service {ServiceId} already exists.", req.ServiceId);
            return ProvisioningResult.AlreadyExists(req.Username, existing.Domain);
        }

        // A lost answer with a lost lookup says nothing about the account: it may exist. Only an
        // answer, a refusal or a positive "not found", may end the order as refused.
        if (!created.IsRefusal && lookup.Outcome is WhmLookupOutcome.Unknown)
        {
            _logger.LogWarning("WHM could not be asked to create the account for service {ServiceId}; its state is unknown.", req.ServiceId);
            throw new WhmUnavailableException("WHM did not answer the account creation and the account could not be looked up.");
        }

        _logger.LogWarning("WHM refused to create the account for service {ServiceId}.", req.ServiceId);
        return ProvisioningResult.Refused(CreateFailedReason);
    }

    /// <summary>Suspends the service's account.</summary>
    /// <param name="req">The service's provisioning reference and the suspension reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses and the account is not already suspended.</exception>
    public async Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        RequireReference(req.ProvisioningRef);
        var reply = await _client.SuspendAccountAsync(_connection, req.ProvisioningRef, req.Reason, ct);
        await EnsureSuspendedAsync(req.ProvisioningRef, reply, true, ct);
    }

    /// <summary>Unsuspends the service's account.</summary>
    /// <param name="provisioningRef">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses and the account is not already active.</exception>
    public async Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        RequireReference(provisioningRef);
        var reply = await _client.UnsuspendAccountAsync(_connection, provisioningRef, ct);
        await EnsureSuspendedAsync(provisioningRef, reply, false, ct);
    }

    /// <summary>Removes the service's account.</summary>
    /// <param name="req">The service's provisioning reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses and does not positively report the account gone.</exception>
    public async Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        RequireReference(req.ProvisioningRef);
        if ((await _client.RemoveAccountAsync(_connection, req.ProvisioningRef, ct)).IsSuccess)
        {
            _logger.LogInformation("cPanel account {User} terminated.", req.ProvisioningRef);
            return;
        }

        var lookup = await _client.FindAccountAsync(_connection, req.ProvisioningRef, ct);
        if (lookup.Outcome is not WhmLookupOutcome.NotFound)
        {
            throw new InvalidOperationException("WHM terminate failed.");
        }

        _logger.LogInformation("cPanel account {User} was already removed.", req.ProvisioningRef);
    }

    /// <summary>Returns the sign-in details of the service's account.</summary>
    /// <param name="provisioningRef">The account's username.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The username, the account's domain, the server and the cPanel address; WHM never reveals a password.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the account cannot be found.</exception>
    public async Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        RequireReference(provisioningRef);
        var account = (await _client.FindAccountAsync(_connection, provisioningRef, ct)).Value
            ?? throw new InvalidOperationException("The hosting account was not found on the server.");

        return new ServiceCredentials(provisioningRef, null, account.Domain, _serverIp, _panelUrl);
    }

    /// <summary>Opens a cPanel session for the service's account and returns its one-time login URL.</summary>
    /// <param name="provisioningRef">The account's username.</param>
    /// <param name="callerIp">Not used: WHM's <c>create_user_session</c> does not bind the session to an address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The URL WHM returned, once it is shown to be an https address on this server.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses the session.</exception>
    /// <exception cref="DomainRuleException">Thrown with <see cref="HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED"/> when the URL WHM returned is not an https address on this server.</exception>
    public async Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        RequireReference(provisioningRef);
        var url = await _client.CreateUserSessionAsync(_connection, provisioningRef, ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("WHM refused to open a cPanel session.");
        }

        // The URL is handed to a browser, so it is only passed on when it points at this server:
        // https, no user information, and the server's own host. Nothing of it is logged.
        if (!WhmInputRules.IsSessionUrlOf(url, _connection.BaseUrl, _serverIp))
        {
            _logger.LogWarning("WHM returned a cPanel session URL that is not on the server for {User}.", provisioningRef);
            throw new DomainRuleException(HostpanelErrorCodes.SERVICE_CPANEL_SSO_URL_UPSTREAM_FAILED, "The cPanel session URL is not on the server.");
        }

        _logger.LogInformation("cPanel session opened for {User}.", provisioningRef);
        return url;
    }

    /// <summary>Changes the service's account password.</summary>
    /// <param name="provisioningRef">The account's username.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses the change.</exception>
    public async Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        RequireReference(provisioningRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        if (!(await _client.ChangePasswordAsync(_connection, provisioningRef, newPassword, ct)).IsSuccess)
        {
            throw new InvalidOperationException("WHM password change failed.");
        }

        _logger.LogInformation("cPanel password changed for {User}.", provisioningRef);
    }

    /// <summary>Moves the service's account onto another WHM package.</summary>
    /// <param name="provisioningRef">The account's username.</param>
    /// <param name="newPackage">The name of the package to switch to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when WHM refuses and the account is not already on the package.</exception>
    public async Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        RequireReference(provisioningRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPackage);
        if ((await _client.ChangePackageAsync(_connection, provisioningRef, newPackage, ct)).IsSuccess)
        {
            _logger.LogInformation("cPanel package changed for {User}.", provisioningRef);
            return;
        }

        var lookup = await _client.FindAccountAsync(_connection, provisioningRef, ct);
        if (!string.Equals(lookup.Value?.Plan, newPackage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("WHM package change failed.");
        }

        _logger.LogInformation("cPanel account {User} was already on the package.", provisioningRef);
    }

    /// <summary>Stops a call that names an account WHM could never hold, before WHM is asked.</summary>
    /// <param name="provisioningRef">The account's username as stored.</param>
    /// <exception cref="DomainRuleException">Thrown with <see cref="HostpanelErrorCodes.CPANEL_USERNAME_INVALID"/> when the value is blank or not a username.</exception>
    private static void RequireReference(string? provisioningRef)
    {
        if (!WhmInputRules.IsAccountReference(provisioningRef))
        {
            throw new DomainRuleException(HostpanelErrorCodes.CPANEL_USERNAME_INVALID, "The account reference is not a valid cPanel username.");
        }
    }

    /// <summary>Accepts a suspend or unsuspend that failed only when the account is already as asked.</summary>
    /// <param name="user">The account's username.</param>
    /// <param name="reply">WHM's answer to the suspend or unsuspend.</param>
    /// <param name="suspended">Whether the account is to be suspended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the account is known to be as asked.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the account is not as asked, or its state cannot be read.</exception>
    private async Task EnsureSuspendedAsync(string user, WhmReply reply, bool suspended, CancellationToken ct)
    {
        if (reply.IsSuccess)
        {
            _logger.LogInformation("cPanel account {User} suspended: {Suspended}.", user, suspended);
            return;
        }

        var lookup = await _client.FindAccountAsync(_connection, user, ct);
        if (lookup.Value?.IsSuspended != suspended)
        {
            throw new InvalidOperationException(suspended ? "WHM suspend failed." : "WHM unsuspend failed.");
        }

        _logger.LogInformation("cPanel account {User} was already suspended: {Suspended}.", user, suspended);
    }
}
