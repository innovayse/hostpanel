using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Events;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="RevokeSshKeysOnPluginRemovedHandler"/> never takes a refused revoke for "nothing to revoke": a refusal or an
/// unconfirmed key fails the delivery, so the durable queue retries it. The bus is Wolverine's own test context, which
/// answers the command the way a refusing or a partly failing handler would.
/// </summary>
public sealed class RevokeSshKeysOnPluginRemovedHandlerTests
{
    /// <summary>A refused command throws instead of logging zero keys revoked.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RefusedCommand_FailsTheDeliveryAsync()
    {
        var bus = new TestMessageContext();
        bus.WhenInvokedMessageOf<RevokePluginIssuedSshKeysCommand>()
            .RespondWith(Result<PluginSshKeyRevocationDto>.FailureCode(HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE));
        var handler = new RevokeSshKeysOnPluginRemovedHandler(bus, NullLogger<RevokeSshKeysOnPluginRemovedHandler>.Instance);

        var act = () => handler.HandleAsync(new PluginRemovedEvent("removed-plugin", DateTimeOffset.UnixEpoch), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A key the agent did not confirm throws too, and a pass with every key revoked completes.</summary>
    /// <param name="failed">How many keys were not confirmed revoked.</param>
    /// <param name="throws">Whether the delivery must fail.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task UnconfirmedKeys_FailTheDelivery_AllRevoked_CompletesAsync(int failed, bool throws)
    {
        var bus = new TestMessageContext();
        bus.WhenInvokedMessageOf<RevokePluginIssuedSshKeysCommand>()
            .RespondWith(Result<PluginSshKeyRevocationDto>.Success(new PluginSshKeyRevocationDto(2, failed)));
        var handler = new RevokeSshKeysOnPluginRemovedHandler(bus, NullLogger<RevokeSshKeysOnPluginRemovedHandler>.Instance);

        var act = () => handler.HandleAsync(new PluginRemovedEvent("removed-plugin", DateTimeOffset.UnixEpoch), CancellationToken.None);

        if (throws)
        {
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        else
        {
            await act.Should().NotThrowAsync();
        }
    }
}
