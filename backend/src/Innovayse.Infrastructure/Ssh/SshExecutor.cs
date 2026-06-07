namespace Innovayse.Infrastructure.Ssh;

using Innovayse.Application.Common;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

/// <summary>SSH.NET implementation of <see cref="ISshExecutor"/>.</summary>
/// <param name="logger">Logger instance.</param>
public sealed class SshExecutor(ILogger<SshExecutor> logger) : ISshExecutor
{
    /// <inheritdoc />
    public async Task<SshResult> ExecuteAsync(
        string host, int port, string username,
        string? password, string? privateKey,
        string script, CancellationToken ct)
    {
        logger.LogInformation("SSH connecting to {Host}:{Port} as {Username}", host, port, username);

        AuthenticationMethod auth = password is not null
            ? new PasswordAuthenticationMethod(username, password)
            : new PrivateKeyAuthenticationMethod(username,
                new PrivateKeyFile(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKey!))));

        var connectionInfo = new ConnectionInfo(host, port, username, auth);

        using var client = new SshClient(connectionInfo);
        await Task.Run(() => client.Connect(), ct);

        try
        {
            var cmd = client.RunCommand(script);
            var exitCode = cmd.ExitStatus ?? -1;
            var success = exitCode == 0;

            if (success)
                logger.LogInformation("SSH command succeeded on {Host} (exit code {Code})", host, exitCode);
            else
                logger.LogWarning("SSH command failed on {Host} (exit code {Code}): {Error}", host, exitCode, cmd.Error);

            return new SshResult(success, cmd.Result, cmd.Error, exitCode);
        }
        finally
        {
            client.Disconnect();
        }
    }
}
