using System.Text;
using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime;
using Wolverine.Util;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Wolverine's PostgreSQL message store as the host runs it: a domain event saved in a committed
/// transaction is persisted as an envelope and handled, a rolled-back one leaves no envelope, a queued
/// handler that fails is retried until it succeeds (alone, when its event has other handlers), and a
/// recurring job's scheduled run stays in memory.
/// </summary>
/// <remarks>
/// The store is read through Wolverine's own admin API (<see cref="IMessageStoreAdmin"/>), never with SQL,
/// so the tests do not depend on the layout of Wolverine's tables. Every host is derived from the shared
/// factory with <see cref="FlakyProbeHandler"/> and the two <see cref="SplitProbeEvent"/> handlers added to
/// discovery and mail sent to a recording relay.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DurableMessagingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>How many times a wait looks before giving up.</summary>
    private const int PollAttempts = 600;

    /// <summary>
    /// The gap between two looks, so a wait is bounded at about one minute: the first delivery of a host
    /// compiles its handler chains, and the flaky probe's retry waits out the first cooldown.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How far ahead the scheduling test schedules, so nothing it schedules runs during the test.</summary>
    private static readonly TimeSpan FarAhead = TimeSpan.FromHours(1);

    /// <summary>Builds a host whose mail lands in <paramref name="emailSender"/> and that also handles the flaky probe.</summary>
    /// <param name="emailSender">The fake relay the host's mail pipeline will use.</param>
    /// <returns>The derived host.</returns>
    private WebApplicationFactory<Program> ProbeHost(FakeEmailSender emailSender) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEmailSender>(emailSender);
                services.ConfigureWolverine(FlakyProbeHandler.IncludeIn);
                services.ConfigureWolverine(SplitProbeFailingHandler.IncludeIn);
            }));

    /// <summary>Every envelope the host's message store holds, incoming (including scheduled and handled) and outgoing.</summary>
    /// <param name="host">The host whose store is read.</param>
    /// <returns>The stored envelopes.</returns>
    private static async Task<List<Envelope>> StoredEnvelopesAsync(WebApplicationFactory<Program> host)
    {
        var admin = host.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin;
        var incoming = await admin.AllIncomingAsync();
        var outgoing = await admin.AllOutgoingAsync();

        return [.. incoming, .. outgoing];
    }

    /// <summary>Whether an envelope carries a <see cref="ClientCreatedEvent"/> naming <paramref name="email"/>.</summary>
    /// <param name="envelope">A stored envelope.</param>
    /// <param name="email">The unique email the test's client was created with.</param>
    /// <returns><see langword="true"/> for the creation event of that client.</returns>
    private static bool IsCreationOf(Envelope envelope, string email) =>
        envelope.MessageType == typeof(ClientCreatedEvent).ToMessageTypeName()
        && envelope.Data is { } data
        && Encoding.UTF8.GetString(data).Contains(email, StringComparison.Ordinal);

    /// <summary>Polls until <paramref name="condition"/> holds or the bounded wait runs out.</summary>
    /// <param name="condition">What to wait for.</param>
    /// <returns><see langword="true"/> when the condition held in time.</returns>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < PollAttempts; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(PollInterval);
        }

        return false;
    }

    /// <summary>
    /// A client saved in a committed transaction leaves its creation event in the message store and the
    /// event is handled (the welcome mail goes out); a client saved in a transaction that rolls back leaves
    /// no envelope behind, because the outbox rows were written in that transaction.
    /// </summary>
    /// <returns>A task that completes when both outcomes were checked.</returns>
    [Fact]
    public async Task CommittedSave_PersistsTheEvent_RolledBackSave_PersistsNothingAsync()
    {
        var emailSender = new FakeEmailSender();
        var host = ProbeHost(emailSender);
        var rolledBackEmail = $"durable-rolled-back-{Guid.NewGuid():N}@example.test";
        var committedEmail = $"durable-committed-{Guid.NewGuid():N}@example.test";

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Clients.Add(Client.Create($"subject-{Guid.NewGuid():N}", "Edsger", "Dijkstra", rolledBackEmail, TestMoments.Seeded));
            await context.SaveChangesAsync(CancellationToken.None);
            await transaction.RollbackAsync();
        }

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Clients.Add(Client.Create($"subject-{Guid.NewGuid():N}", "Barbara", "Liskov", committedEmail, TestMoments.Seeded));
            await context.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync();
        }

        (await WaitUntilAsync(() => emailSender.Sent.Any(m => m.To == committedEmail)))
            .Should().BeTrue("the committed save's event must be delivered to the welcome handler");

        // Handled incoming envelopes are kept for a while (KeepAfterMessageHandling) before Wolverine
        // deletes them, so the committed event is still visible here once its handler has run.
        var stored = await StoredEnvelopesAsync(host);
        stored.Should().Contain(e => IsCreationOf(e, committedEmail), "a committed event is written to the durable store");
        stored.Should().NotContain(e => IsCreationOf(e, rolledBackEmail), "a rolled-back save takes its outbox rows with it");
        emailSender.Sent.Should().NotContain(m => m.To == rolledBackEmail);
    }

    /// <summary>
    /// A message delivered from a durable local queue whose handler throws a transient fault is not lost:
    /// the retry rules run it again and the second attempt succeeds, with nothing sent to the dead letters.
    /// </summary>
    /// <returns>A task that completes when the retry was observed.</returns>
    [Fact]
    public async Task QueuedHandlerThatThrows_IsRetried_AndSucceedsAsync()
    {
        var host = ProbeHost(new FakeEmailSender());
        var probeId = Guid.NewGuid();

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new FlakyProbeMessage(probeId));
        }

        (await WaitUntilAsync(() => FlakyProbeHandler.HasSucceeded(probeId)))
            .Should().BeTrue("the first attempt threw a transient fault, so the message must be retried");
        FlakyProbeHandler.AttemptsFor(probeId).Should().Be(2, "one failed attempt, then one that succeeded");

        var counts = await host.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin.FetchCountsAsync();
        counts.DeadLetter.Should().Be(0, "a message that succeeded on retry never reaches the dead letters");
    }

    /// <summary>
    /// An event with two handlers runs each on its own chain: the handler whose first attempt throws is
    /// retried until it succeeds, while its sibling, which succeeded at once, runs exactly once. Under one
    /// combined chain the retry would have replayed the sibling too.
    /// </summary>
    /// <returns>A task that completes when the retry was observed and the sibling counted.</returns>
    [Fact]
    public async Task EventWithTwoHandlers_RetriesOnlyTheHandlerThatFailedAsync()
    {
        var host = ProbeHost(new FakeEmailSender());
        var probeId = Guid.NewGuid();

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new SplitProbeEvent(probeId));
        }

        (await WaitUntilAsync(() => SplitProbeFailingHandler.HasSucceeded(probeId)))
            .Should().BeTrue("the failing handler's first attempt threw a transient fault, so it must be retried");
        (await WaitUntilAsync(() => SplitProbeSteadyHandler.RunsFor(probeId) > 0))
            .Should().BeTrue("the sibling handler receives the event on its own chain");

        SplitProbeFailingHandler.AttemptsFor(probeId).Should().Be(2, "one failed attempt, then one that succeeded");
        SplitProbeSteadyHandler.RunsFor(probeId).Should().Be(1, "the sibling's chain was never retried, so it ran once");
    }

    /// <summary>
    /// A recurring job scheduled with <c>ScheduleAsync</c> is held in memory, not written to the durable
    /// store: the chain re-creates itself on every start, so a stored run would add a second chain after
    /// each restart. A durable message scheduled the same way is stored, which proves the read sees
    /// scheduled envelopes at all.
    /// </summary>
    /// <returns>A task that completes when the store was read.</returns>
    [Fact]
    public async Task ScheduledRecurringJob_IsNotWrittenToTheDurableStoreAsync()
    {
        var host = ProbeHost(new FakeEmailSender());

        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.ScheduleAsync(new ReconcileGatewayPaymentsCronCommand(), FarAhead);
            await bus.ScheduleAsync(new FlakyProbeMessage(Guid.NewGuid()), FarAhead);
        }

        var stored = await StoredEnvelopesAsync(host);
        stored.Should().Contain(
            e => e.MessageType == typeof(FlakyProbeMessage).ToMessageTypeName() && e.Status == EnvelopeStatus.Scheduled,
            "a message on a durable queue scheduled for later is stored");
        stored.Should().NotContain(
            e => e.MessageType == typeof(ReconcileGatewayPaymentsCronCommand).ToMessageTypeName(),
            "recurring jobs run on the in-memory queue, so their scheduled runs never reach the store");
    }
}
