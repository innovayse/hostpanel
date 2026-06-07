namespace Innovayse.Application.Common;

/// <summary>Executes shell commands on a remote server via SSH.</summary>
public interface ISshExecutor
{
    /// <summary>
    /// Connects to a remote server and executes a shell script.
    /// </summary>
    /// <param name="host">Server IP or hostname.</param>
    /// <param name="port">SSH port (default 22).</param>
    /// <param name="username">SSH username (typically root).</param>
    /// <param name="password">SSH password or null if using key auth.</param>
    /// <param name="privateKey">Private key string or null if using password auth.</param>
    /// <param name="script">Shell script to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Command output and success status.</returns>
    Task<SshResult> ExecuteAsync(
        string host, int port, string username,
        string? password, string? privateKey,
        string script, CancellationToken ct);
}

/// <summary>Result of an SSH command execution.</summary>
/// <param name="Success">True if exit code was 0.</param>
/// <param name="Output">Standard output.</param>
/// <param name="Error">Standard error output.</param>
/// <param name="ExitCode">Process exit code.</param>
public record SshResult(bool Success, string Output, string Error, int ExitCode);
