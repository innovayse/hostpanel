using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>GetEmailDelivery</c> use case, driven through the bus the way a caller that sent a keyed
/// mail without waiting does: the log line tells how it ended, and a key nothing was logged under is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetEmailDeliveryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Writes a log line under a fresh key.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="failure">The relay's refusal, or <see langword="null"/> for a delivered mail.</param>
    /// <returns>The key the line was written under.</returns>
    private static async Task<string> LogAsync(IServiceProvider services, EmailSendFailure? failure)
    {
        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var log = EmailLog.Claim("someone@example.com", "Subject", "Body", key, TestMoments.Seeded);
        if (failure is { } refused)
        {
            log.MarkUndelivered(refused, TestMoments.Seeded);
        }
        else
        {
            log.MarkDelivered(TestMoments.Seeded);
        }

        var context = services.GetRequiredService<IHostpanelDbContext>();
        context.EmailLogs.Add(log);
        await context.SaveChangesAsync(CancellationToken.None);
        return key;
    }

    /// <summary>Asks the bus how a mail ended.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="key">The mail's key.</param>
    /// <returns>The query's outcome.</returns>
    private static Task<Result<EmailDeliveryDto>> GetAsync(IServiceProvider services, string key) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result<EmailDeliveryDto>>(new GetEmailDeliveryQuery(key), CancellationToken.None);

    /// <summary>A delivered mail reads as sent, with no failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_DeliveredMail_ReadsAsSent()
    {
        using var scope = factory.Services.CreateScope();
        var key = await LogAsync(scope.ServiceProvider, null);

        var result = await GetAsync(scope.ServiceProvider, key);

        result.Value!.State.Should().Be(EmailDeliveryState.Sent);
        result.Value.Failure.Should().BeNull();
        result.Value.MayResend.Should().BeFalse();
    }

    /// <summary>A refused mail reads as failed, with the classified reason.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_RefusedMail_ReadsAsFailedWithTheReason()
    {
        using var scope = factory.Services.CreateScope();
        var key = await LogAsync(scope.ServiceProvider, EmailSendFailure.Timeout);

        var result = await GetAsync(scope.ServiceProvider, key);

        result.Value!.State.Should().Be(EmailDeliveryState.Failed);
        result.Value.Failure.Should().Be(EmailSendFailure.Timeout);
        result.Value.MayResend.Should().BeTrue();
    }

    /// <summary>A key nothing was logged under is refused with <c>EMAIL_LOG_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownKey_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();

        var result = await GetAsync(scope.ServiceProvider, "unknown-key");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EMAIL_LOG_NOT_FOUND);
    }
}
