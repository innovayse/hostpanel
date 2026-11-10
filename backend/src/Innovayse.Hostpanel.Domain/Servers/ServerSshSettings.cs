using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// Per-server configuration of SSH key management: what customers are told to connect to, and
/// how HostPanel reaches the key agent installed on that server.
/// </summary>
/// <remarks>
/// <para>
/// One row per <see cref="Server"/>, keyed by <see cref="ServerId"/>, rather than more
/// columns on <c>servers</c>: most servers will never have it, and the agent credential is a
/// different secret from the control-panel API key with a different blast radius.
/// </para>
/// <para>
/// A server without a row, or with <see cref="IsEnabled"/> false, offers no SSH key management.
/// Nothing turns it on automatically.
/// </para>
/// </remarks>
public sealed class ServerSshSettings : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the server this configuration belongs to.</summary>
    public int ServerId { get; private set; }

    /// <summary>Gets whether customers on this server may manage SSH keys.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Gets the hostname customers connect to with <c>ssh</c>.</summary>
    public string SshHostname { get; private set; } = string.Empty;

    /// <summary>Gets the port customers connect to with <c>ssh</c>.</summary>
    public int SshPort { get; private set; } = 22;

    /// <summary>Gets the host HostPanel connects to for agent calls.</summary>
    public string AgentHost { get; private set; } = string.Empty;

    /// <summary>Gets the SSH port HostPanel connects to for agent calls.</summary>
    public int AgentPort { get; private set; } = 22;

    /// <summary>Gets the management user whose only authorized key forces the agent command.</summary>
    public string AgentUsername { get; private set; } = string.Empty;

    /// <summary>Gets the OpenSSH private key HostPanel authenticates with. Encrypted at rest.</summary>
    public string? AgentPrivateKey { get; private set; }

    /// <summary>
    /// Gets the public half of <see cref="AgentPrivateKey"/> when HostPanel generated the pair, for
    /// the installer command. Null for a key an administrator pasted in.
    /// </summary>
    public string? AgentPublicKey { get; private set; }

    /// <summary>
    /// Gets the id that ties the agent installed on the server to this server record. The installer
    /// writes it into the agent's config and the agent reports it on ping; a mismatch means the
    /// record points at an agent installed for another server.
    /// </summary>
    public Guid? AgentBinding { get; private set; }

    /// <summary>
    /// Gets the pinned SHA256 fingerprint of the server's host key. Calls are refused when the
    /// presented host key does not match, so a spoofed host cannot collect requests.
    /// </summary>
    public string AgentHostKeyFingerprint { get; private set; } = string.Empty;

    /// <summary>Gets the connection timeout in seconds.</summary>
    public int ConnectTimeoutSeconds { get; private set; } = 10;

    /// <summary>Gets the per-operation timeout in seconds.</summary>
    public int OperationTimeoutSeconds { get; private set; } = 20;

    /// <summary>Gets the result of the last health check, if one ran.</summary>
    public bool? IsHealthy { get; private set; }

    /// <summary>Gets when the last health check ran.</summary>
    public DateTimeOffset? LastHealthCheckAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ServerSshSettings() { }

    /// <summary>Creates disabled settings for a server; <see cref="Configure"/> fills them in.</summary>
    /// <param name="serverId">The server.</param>
    /// <returns>The new, disabled settings.</returns>
    public static ServerSshSettings CreateFor(int serverId) => new() { ServerId = serverId };

    /// <summary>Replaces the configuration.</summary>
    /// <param name="isEnabled">Whether customers may manage keys.</param>
    /// <param name="sshHostname">Customer-facing hostname.</param>
    /// <param name="sshPort">Customer-facing port.</param>
    /// <param name="agentHost">Agent host.</param>
    /// <param name="agentPort">Agent SSH port.</param>
    /// <param name="agentUsername">Management user.</param>
    /// <param name="agentPrivateKey">New private key, or <see langword="null"/> to keep the stored one.</param>
    /// <param name="agentHostKeyFingerprint">Pinned host key fingerprint.</param>
    /// <param name="connectTimeoutSeconds">Connect timeout.</param>
    /// <param name="operationTimeoutSeconds">Operation timeout.</param>
    /// <exception cref="ArgumentException">Thrown when an enabled configuration is incomplete.</exception>
    public void Configure(
        bool isEnabled, string sshHostname, int sshPort,
        string agentHost, int agentPort, string agentUsername, string? agentPrivateKey,
        string agentHostKeyFingerprint, int connectTimeoutSeconds, int operationTimeoutSeconds)
    {
        if (sshPort is < 1 or > 65535 || agentPort is < 1 or > 65535)
        {
            throw new ArgumentException("Port must be between 1 and 65535.");
        }

        if (connectTimeoutSeconds is < 1 or > 120 || operationTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentException("Timeouts are out of range.");
        }

        // A changed endpoint, user or pinned host key means the last health check no longer
        // describes what HostPanel will connect to.
        if (!string.Equals(AgentHost, agentHost.Trim(), StringComparison.OrdinalIgnoreCase)
            || AgentPort != agentPort
            || AgentUsername != agentUsername.Trim()
            || AgentHostKeyFingerprint != agentHostKeyFingerprint.Trim())
        {
            IsHealthy = null;
        }

        IsEnabled = isEnabled;
        SshHostname = sshHostname.Trim();
        SshPort = sshPort;
        AgentHost = agentHost.Trim();
        AgentPort = agentPort;
        AgentUsername = agentUsername.Trim();
        if (agentPrivateKey is not null && agentPrivateKey != AgentPrivateKey)
        {
            AgentPrivateKey = agentPrivateKey;
            AgentPublicKey = null;
            IsHealthy = null;
        }

        AgentHostKeyFingerprint = agentHostKeyFingerprint.Trim();
        ConnectTimeoutSeconds = connectTimeoutSeconds;
        OperationTimeoutSeconds = operationTimeoutSeconds;

        if (isEnabled && !IsComplete)
        {
            throw new ArgumentException("Enabled SSH settings need a hostname, agent host, agent user, private key and host key fingerprint.");
        }
    }

    /// <summary>Gets whether every value an agent call needs is present.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(SshHostname)
        && !string.IsNullOrWhiteSpace(AgentHost)
        && !string.IsNullOrWhiteSpace(AgentUsername)
        && !string.IsNullOrWhiteSpace(AgentPrivateKey)
        && !string.IsNullOrWhiteSpace(AgentHostKeyFingerprint);

    /// <summary>
    /// Replaces the management identity with a newly generated pair. The binding is created once and
    /// kept across rotations, so a reinstall with the new key stays tied to this record. Rotation
    /// switches customer access off and clears the health check: the server holds the old key until
    /// the installer runs again.
    /// </summary>
    /// <param name="privateKey">OpenSSH private key.</param>
    /// <param name="publicKey">Matching public key line.</param>
    public void IssueIdentity(string privateKey, string publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        AgentPrivateKey = privateKey;
        AgentPublicKey = publicKey;
        AgentBinding ??= Guid.NewGuid();
        IsEnabled = false;
        IsHealthy = null;
        LastHealthCheckAt = null;
    }

    /// <summary>Records a health check outcome.</summary>
    /// <param name="healthy">Whether the agent answered a ping.</param>
    /// <param name="checkedAt">When the check ran, UTC.</param>
    public void RecordHealthCheck(bool healthy, DateTimeOffset checkedAt)
    {
        IsHealthy = healthy;
        LastHealthCheckAt = checkedAt;
    }
}
