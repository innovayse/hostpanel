namespace Innovayse.Hostpanel.Domain.Ssh.Enums;

/// <summary>
/// Why an SSH key provider call failed. Carried by <see cref="SshProviderException"/> and
/// recorded on an <see cref="SshKey"/> whose install or revoke failed.
/// </summary>
/// <remarks>
/// The provider adapter maps its own wire codes to these values; anything it does not recognise
/// becomes <see cref="Unknown"/>. Only <see cref="Unreachable"/>, <see cref="Timeout"/> and
/// <see cref="InternalError"/> leave the outcome on the server unknown; every other value is a
/// definite refusal.
/// </remarks>
public enum SshProviderFailure
{
    /// <summary>The provider answered with a reason this version does not know.</summary>
    Unknown,

    /// <summary>The server could not be reached or the connection dropped.</summary>
    Unreachable,

    /// <summary>The call timed out; it may have been applied.</summary>
    Timeout,

    /// <summary>The provider failed internally or answered something unreadable.</summary>
    InternalError,

    /// <summary>The provider speaks another contract version.</summary>
    UnsupportedVersion,

    /// <summary>The provider rejected the request as malformed.</summary>
    InvalidRequest,

    /// <summary>The provider does not know the requested operation.</summary>
    UnknownOperation,

    /// <summary>The request exceeded the provider's size limit.</summary>
    RequestTooLarge,

    /// <summary>The account name is not valid.</summary>
    InvalidAccount,

    /// <summary>The account does not exist on the server.</summary>
    AccountNotFound,

    /// <summary>The account exists but may not have managed keys.</summary>
    AccountNotEligible,

    /// <summary>Shell access is disabled for the account.</summary>
    SshDisabled,

    /// <summary>The public key is not valid.</summary>
    InvalidKey,

    /// <summary>The key is already in the account's authorized keys.</summary>
    DuplicateKey,

    /// <summary>The key id is already used by a different key.</summary>
    KeyIdConflict,

    /// <summary>The account reached its key limit.</summary>
    TooManyKeys,

    /// <summary>The account's key file path is not safe to write.</summary>
    UnsafePath,

    /// <summary>The account's key file exceeds the provider's size limit.</summary>
    FileTooLarge,

    /// <summary>The account is suspended on the server (its password is locked) and may not receive keys.</summary>
    AccountSuspended,

    /// <summary>
    /// HostPanel could not ask the server at all: it no longer exists or has no complete agent
    /// settings. Recorded by HostPanel itself, never answered by an agent.
    /// </summary>
    AgentNotConfigured,

    /// <summary>
    /// The agent answered but was installed for another server record: the binding it reports is not
    /// this server's. Recorded by HostPanel's health check, never answered by an agent.
    /// </summary>
    BindingMismatch,

    /// <summary>The agent does not offer the operation on this server (for example site preparation is not configured).</summary>
    NotSupported,

    /// <summary>The domain asked for is not one of the account's own; nothing was changed.</summary>
    SiteNotOwned,

    /// <summary>Preparing the account's site for deploys failed on the server.</summary>
    SitePrepareFailed,
}
