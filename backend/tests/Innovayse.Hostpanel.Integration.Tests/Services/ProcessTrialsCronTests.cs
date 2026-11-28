using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;
using Innovayse.Hostpanel.Application.Services.Commands.ProcessTrialsCron;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="ProcessTrialsCronHandler"/> against Postgres: it takes the daily step of every running trial
/// (active or awaiting payment) and leaves ordinary and converted services alone, a trial whose step throws does not
/// stop the others, and the job schedules its next run. Every test reads back only its own services, since the cron
/// sweeps the whole table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProcessTrialsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Exactly the running trials of the test (active, pending or awaiting payment) get a step: an ended active trial is
    /// converted (it gets its invoice), an awaiting-payment trial past its due date is terminated, an ended pending trial
    /// is terminated, and an ordinary service and a converted one get no step and are untouched.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrialsCron_ProcessesEveryRunningTrial()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var ended = TestMoments.Seeded.AddDays(-1);
        var (activeClient, active, _) = await SeedAsync(host, new TrialSeed(EndsAt: ended));
        var (_, awaiting, _) = await SeedAsync(host, new TrialSeed(EndsAt: ended));
        var (_, pending, _) = await SeedAsync(host, new TrialSeed(Activate: false, EndsAt: ended));
        var (_, ordinary, _) = await SeedAsync(host, new TrialSeed(EndsAt: ended));
        var (_, converted, _) = await SeedAsync(host, new TrialSeed(EndsAt: ended));
        await SetAsync(host, awaiting, s => s
            .SetProperty(x => x.TrialStatus, TrialStatus.AwaitingPayment)
            .SetProperty(x => x.TrialPaymentDueAt, TestMoments.Seeded.AddDays(-1)));
        await SetAsync(host, ordinary, s => s
            .SetProperty(x => x.TrialStatus, (TrialStatus?)null)
            .SetProperty(x => x.TrialEndsAt, (DateTimeOffset?)null));
        await SetAsync(host, converted, s => s.SetProperty(x => x.TrialStatus, TrialStatus.ConvertedToPaid));

        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => RunAsync(host)));

        int[] seeded = [active, awaiting, pending, ordinary, converted];
        session.Executed.MessagesOf<ProcessTrialCommand>().Select(c => c.ServiceId).Where(seeded.Contains)
            .Should().BeEquivalentTo(new[] { active, awaiting, pending });
        (await StatusAsync(host, active)).Should().Be(TrialStatus.AwaitingPayment);
        (await InvoiceCountAsync(host, activeClient)).Should().Be(1);
        (await StatusAsync(host, awaiting)).Should().Be(TrialStatus.Terminated);
        (await StatusAsync(host, pending)).Should().Be(TrialStatus.Terminated);
        (await StatusAsync(host, ordinary)).Should().BeNull();
        (await StatusAsync(host, converted)).Should().Be(TrialStatus.ConvertedToPaid);
    }

    /// <summary>One trial whose step throws does not stop the run: another trial is still processed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrialsCron_OneFailingTrial_DoesNotStopTheOthers()
    {
        var sender = new FakeEmailSender();
        var poisoned = new PoisonedTrial();
        using var host = new ClockedHost(factory, configure: b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new PoisonedTrialSaveInterceptor(poisoned)));
        }));
        var endsAt = TestMoments.Seeded.AddDays(3);
        var (_, bad, _) = await SeedAsync(host, new TrialSeed(EndsAt: endsAt));
        var (_, good, _) = await SeedAsync(host, new TrialSeed(EndsAt: endsAt));
        poisoned.ServiceId = bad;

        await TrialTestSeeds.SettleAsync(host.Services, async () =>
        {
            await RunAsync(host);
            return true;
        });

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.TrialReminders.AsNoTracking().AnyAsync(r => r.ServiceId == good)).Should().BeTrue();
        (await context.TrialReminders.AsNoTracking().AnyAsync(r => r.ServiceId == bad)).Should().BeFalse();
    }

    /// <summary>The job schedules its next run, once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrialsCron_ReschedulesItself()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());

        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new ProcessTrialsCronCommand());

        var handled = session.AllRecordsInOrder()
            .FirstOrDefault(r => r.MessageEventType == MessageEventType.ExecutionStarted
                                 && r.Envelope?.Message?.GetType() == typeof(ProcessTrialsCronCommand))
            ?.Envelope?.Id;
        session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == typeof(ProcessTrialsCronCommand) && e.Id != handled)
            .Select(e => e.Id)
            .Distinct()
            .Should().ContainSingle();
    }

    /// <summary>Seeds a trial and waits for the events of the seeding.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="seed">The trial rules and the state of the service.</param>
    /// <returns>The client id, the service id and the client's email address.</returns>
    private static Task<(int ClientId, int ServiceId, string Email)> SeedAsync(ClockedHost host, TrialSeed seed) =>
        TrialTestSeeds.SettleAsync(host.Services, () => TrialTestSeeds.SeedTrialAsync(host.Services, seed));

    /// <summary>Runs the cron once on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <returns>A task.</returns>
    private static async Task RunAsync(ClockedHost host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ProcessTrialsCronCommand(), CancellationToken.None);
    }

    /// <summary>Overwrites trial columns of a seeded service.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="set">The column assignments.</param>
    /// <returns>A task.</returns>
    private static async Task SetAsync(
        ClockedHost host,
        int serviceId,
        System.Linq.Expressions.Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<ClientService>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<ClientService>>> set)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.ClientServices.Where(x => x.Id == serviceId).ExecuteUpdateAsync(set, CancellationToken.None);
    }

    /// <summary>Reads a service's trial status.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The trial status.</returns>
    private static async Task<TrialStatus?> StatusAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().Where(s => s.Id == serviceId)
            .Select(s => s.TrialStatus).SingleAsync(CancellationToken.None);
    }

    /// <summary>Counts a client's invoices.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The count.</returns>
    private static async Task<int> InvoiceCountAsync(ClockedHost host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId, CancellationToken.None);
    }

    /// <summary>Names the one trial whose reminder save <see cref="PoisonedTrialSaveInterceptor"/> refuses.</summary>
    private sealed class PoisonedTrial
    {
        /// <summary>Gets or sets the poisoned service id.</summary>
        public int ServiceId { get; set; }
    }

    /// <summary>A save interceptor that throws when a reminder row for the poisoned trial is about to be saved.</summary>
    /// <param name="poisoned">The poisoned trial.</param>
    private sealed class PoisonedTrialSaveInterceptor(PoisonedTrial poisoned) : SaveChangesInterceptor
    {
        /// <inheritdoc/>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } db
                && db.ChangeTracker.Entries<TrialReminder>().Any(e => e.State == EntityState.Added && e.Entity.ServiceId == poisoned.ServiceId))
            {
                throw new InvalidOperationException("Simulated failure for the poisoned trial.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
