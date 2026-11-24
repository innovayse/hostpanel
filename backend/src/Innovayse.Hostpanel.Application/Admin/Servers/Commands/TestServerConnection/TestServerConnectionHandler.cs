using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.TestServerConnection;

/// <summary>Handles <see cref="TestServerConnectionCommand"/>: tests connectivity and persists the result.</summary>
public sealed class TestServerConnectionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Probes a server's control-panel API to confirm the stored credentials still work.</summary>
    private readonly IServerConnectionTester _tester;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Turns a probe's error code into the sentence in the request's language.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="tester">Probes a server's control-panel API to confirm the stored credentials still work.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="messages">Turns a probe's error code into the sentence in the request's language.</param>
    public TestServerConnectionHandler(
        IHostpanelDbContext context, IServerConnectionTester tester, IClock clock, IErrorMessageResolver messages)
    {
        _context = context;
        _tester = tester;
        _clock = clock;
        _messages = messages;
    }

    /// <summary>
    /// Runs the connection test, updates the server's status, and returns the result.
    /// </summary>
    /// <param name="command">The command containing the server id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The test result, whether or not the connection itself succeeded, or
    /// <c>SERVER_NOT_FOUND</c> when the id names no server. A failed connection is not a refusal
    /// of this request; the diagnostic result is the answer the caller asked for.
    /// </returns>
    public async Task<Result<TestServerConnectionResultDto>> HandleAsync(
        TestServerConnectionCommand command, CancellationToken ct)
    {
        var server = await _context.Servers.FirstOrDefaultAsync(s => s.Id == command.ServerId, ct);
        if (server is null)
        {
            return Result<TestServerConnectionResultDto>.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        var probe = await _tester.TestAsync(server, ct);

        server.RecordConnectionTest(probe.Connected, probe.AccountsCount, _clock.UtcNow);
        await _context.SaveChangesAsync(ct);

        return Result<TestServerConnectionResultDto>.Success(new TestServerConnectionResultDto(
            probe.Connected,
            probe.AccountsCount,
            probe.Version,
            probe.ErrorCode,
            probe.ErrorCode is null ? null : _messages.Resolve(probe.ErrorCode),
            server.LastTestedAt!.Value));
    }
}
