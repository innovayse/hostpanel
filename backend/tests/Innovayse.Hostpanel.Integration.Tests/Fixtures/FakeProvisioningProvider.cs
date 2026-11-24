using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IProvisioningProvider"/> standing in for the real control-panel
/// provider a test must not call. Succeeds by default; setting <see cref="ShouldFail"/> makes
/// every member throw, the way an unreachable panel or a rejected request would.
/// </summary>
public sealed class FakeProvisioningProvider : IProvisioningProvider
{
    /// <summary>
    /// When <see langword="true"/>, every member throws instead of answering: the shape an
    /// upstream provider failure takes, whatever its real cause.
    /// </summary>
    public bool ShouldFail { get; set; }

    /// <summary>
    /// When set, every member throws this exception: the shape a real provider's own coded refusal
    /// takes, such as a username the panel can never accept or a plugin that is switched off.
    /// </summary>
    public Exception? FailWith { get; set; }

    /// <summary>
    /// The answer <see cref="ProvisionAsync"/> gives when <see cref="ShouldFail"/> is not set; a
    /// created account by default. Set it to stage an existing account or a reference-less success.
    /// </summary>
    public ProvisioningResult ProvisionAnswer { get; set; } = ProvisioningResult.Created("fake-ref-123");

    /// <summary>
    /// When <see langword="true"/>, only <see cref="GetAccountAsync"/> throws: the panel can create, change and suspend but cannot
    /// be read back.
    /// </summary>
    public bool FailAccountRead { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the members that change the account (suspend, unsuspend, change password and change package)
    /// throw while <see cref="GetAccountAsync"/> still answers: the panel can be read but refuses every change.
    /// </summary>
    public bool FailChanges { get; set; }

    /// <summary>
    /// The answer <see cref="GetAccountAsync"/> gives when no failure is set; an active account that reports nothing else by default.
    /// </summary>
    public HostedAccountState AccountAnswer { get; set; } = new(HostedAccountStatus.Active, null, null, null);

    /// <summary>How many times <see cref="ProvisionAsync"/> was called.</summary>
    public int ProvisionCalls { get; private set; }

    /// <summary>How many times <see cref="SuspendAsync"/> was called.</summary>
    public int SuspendCalls { get; private set; }

    /// <summary>How many times <see cref="GetCredentialsAsync"/> was called.</summary>
    public int CredentialsCalls { get; private set; }

    /// <summary>How many times <see cref="GetCPanelSsoUrlAsync"/> was called.</summary>
    public int SsoCalls { get; private set; }

    /// <summary>How many times <see cref="ChangePasswordAsync"/> was called.</summary>
    public int ChangePasswordCalls { get; private set; }

    /// <summary>How many times <see cref="ChangePackageAsync"/> was called.</summary>
    public int ChangePackageCalls { get; private set; }

    /// <summary>How many times <see cref="GetAccountAsync"/> was called.</summary>
    public int AccountStateCalls { get; private set; }

    /// <summary>The package every <see cref="ChangePackageAsync"/> call was asked to assign, in order.</summary>
    public List<string> ChangedPackages { get; } = [];

    /// <summary>How many times <see cref="UnsuspendAsync"/> was called.</summary>
    public int UnsuspendCalls { get; private set; }

    /// <summary>How many calls reached the account-level members the client and staff screens use.</summary>
    public int AccountCalls => CredentialsCalls + SsoCalls + ChangePasswordCalls + ChangePackageCalls;

    /// <inheritdoc/>
    public Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        ProvisionCalls++;
        ThrowIfFailWith();
        return ShouldFail
            ? Task.FromResult(ProvisioningResult.Refused("fake provider refused to provision"))
            : Task.FromResult(ProvisionAnswer);
    }

    /// <inheritdoc/>
    public Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        SuspendCalls++;
        ThrowIfShouldFail();
        ThrowIfFailChanges();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsuspendAsync(UnsuspendAccountOrder req, CancellationToken ct)
    {
        UnsuspendCalls++;
        ThrowIfShouldFail();
        ThrowIfFailChanges();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        ThrowIfShouldFail();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServiceCredentials> GetCredentialsAsync(AccountCredentialsOrder req, CancellationToken ct)
    {
        CredentialsCalls++;
        ThrowIfShouldFail();
        return Task.FromResult(new ServiceCredentials("fake-user", "fake-pass", "fake.example.com", "1.2.3.4", "https://panel.fake.example.com"));
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(ControlPanelSsoOrder req, CancellationToken ct)
    {
        SsoCalls++;
        ThrowIfShouldFail();
        return Task.FromResult("https://fake-panel.example.com/sso");
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(ChangeAccountPasswordOrder req, CancellationToken ct)
    {
        ChangePasswordCalls++;
        ThrowIfShouldFail();
        ThrowIfFailChanges();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ChangePackageAsync(ChangeAccountPackageOrder req, CancellationToken ct)
    {
        ChangePackageCalls++;
        ChangedPackages.Add(req.NewPackage);
        ThrowIfShouldFail();
        ThrowIfFailChanges();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<HostedAccountState> GetAccountAsync(AccountStateOrder req, CancellationToken ct)
    {
        AccountStateCalls++;
        ThrowIfShouldFail();
        if (FailAccountRead)
        {
            throw new InvalidOperationException("fake provider cannot read the account");
        }

        return Task.FromResult(AccountAnswer);
    }

    /// <summary>Throws when <see cref="FailChanges"/> is set, simulating a panel that refuses every change.</summary>
    private void ThrowIfFailChanges()
    {
        if (FailChanges)
        {
            throw new InvalidOperationException("fake provider refused the change");
        }
    }

    /// <summary>Throws <see cref="FailWith"/> when it is set, simulating a provider's coded refusal.</summary>
    private void ThrowIfFailWith()
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }
    }

    /// <summary>Throws when <see cref="FailWith"/> or <see cref="ShouldFail"/> is set, simulating an upstream refusal.</summary>
    private void ThrowIfShouldFail()
    {
        ThrowIfFailWith();
        if (ShouldFail)
        {
            throw new InvalidOperationException("fake provider refused the request");
        }
    }
}
