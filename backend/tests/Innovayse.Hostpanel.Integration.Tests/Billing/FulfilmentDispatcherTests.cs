using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="FulfilmentDispatcher"/> against Postgres on a clock the test moves by hand: a record is
/// delivered and marked done, the three kinds go out in order, a failure backs off and ends after the last
/// attempt, the error text is cut, a claim holds its lease, and one plugin failing never stops the pass.
/// </summary>
/// <remarks>
/// Each test mints its own plugin id and registers only that plugin on its host, so the records other tests
/// left in the shared database name a plugin this host does not resolve and are never touched.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfilmentDispatcherTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A due record is delivered once with the invoice context and marked done.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DueRecord_IsDeliveredWithTheInvoiceContextAndMarkedDone()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().Equal([("OnInvoicePaidAsync", invoiceId)]);
        var paid = scenario.Plugin.LastPaid!;
        paid.TransactionReference.Should().Be(FulfilmentInvoiceSeeds.PaymentReference);
        paid.Lines.Should().ContainSingle().Which.Currency.Should().Be(FulfilmentInvoiceSeeds.Currency);
        var record = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        record.Status.Should().Be(FulfilmentStatus.Done);
        record.Attempts.Should().Be(1);
        record.CompletedAt.Should().NotBeNull();
        record.LastError.Should().BeNull();
    }

    /// <summary>A record already done is not delivered again by a later pass.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DoneRecord_IsNotDeliveredAgain()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        await scenario.RunPassAsync();

        scenario.Clock.Advance(TimeSpan.FromHours(3));
        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().HaveCount(1);
    }

    /// <summary>The three kinds of one invoice go out paid, refunded, lapsed whatever order they were written in.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OneInvoice_IsDeliveredPaidThenRefundedThenLapsed()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Lapsed, FulfilmentKind.Refunded, FulfilmentKind.Paid);

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Select(c => c.Method).Should().Equal(
            "OnInvoicePaidAsync", "OnInvoiceRefundedAsync", "OnSubscriptionLapsedAsync");
        scenario.Plugin.LastRefunded!.RefundedAmount.Should().Be(FulfilmentInvoiceSeeds.RefundedAmount);
        scenario.Plugin.LastLapsed!.InvoiceId.Should().Be(invoiceId);
    }

    /// <summary>
    /// While the paid record is waiting out a back-off, the refunded one is not delivered ahead of it; once
    /// the paid record is done the refunded one follows.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task LaterKind_WaitsForAnEarlierKindThatFailed()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Refunded, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (method, _, _) =>
            method == "OnInvoicePaidAsync" ? Task.FromException(new InvalidOperationException("down")) : Task.CompletedTask;

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Select(c => c.Method).Should().Equal("OnInvoicePaidAsync");
        (await scenario.ReadAsync(invoiceId, FulfilmentKind.Refunded)).Attempts.Should().Be(0);

        scenario.Plugin.Behaviour = null;
        scenario.Clock.Advance(FulfilmentRecord.InitialBackoff);
        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Select(c => c.Method).Should().Equal(
            "OnInvoicePaidAsync", "OnInvoicePaidAsync", "OnInvoiceRefundedAsync");
    }

    /// <summary>Different invoices and plugins are independent: one invoice waiting does not hold another.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AnotherInvoice_IsNotHeldBackByAFailingOne()
    {
        using var scenario = new FulfilmentScenario(factory);
        var failing = await scenario.SeedInvoiceAsync();
        var healthy = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(failing, FulfilmentKind.Paid);
        await scenario.SeedRecordsAsync(healthy, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (_, invoiceId, _) =>
            invoiceId == failing ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().HaveCount(2, "the failing delivery must not stop the pass");
        var failed = await scenario.ReadAsync(failing, FulfilmentKind.Paid);
        failed.Status.Should().Be(FulfilmentStatus.Pending);
        failed.LastError.Should().Contain("boom");
        (await scenario.ReadAsync(healthy, FulfilmentKind.Paid)).Status.Should().Be(FulfilmentStatus.Done);
    }

    /// <summary>A failing plugin is retried after 30 seconds, then 60, doubling, and not sooner.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailedAttempt_IsRetriedAfterADoublingBackoff()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (_, _, _) => Task.FromException(new InvalidOperationException("down"));
        var failedAt = scenario.Clock.GetUtcNow();

        await scenario.RunPassAsync();
        var first = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Clock.Advance(TimeSpan.FromSeconds(29));
        await scenario.RunPassAsync();
        var tooSoon = scenario.Plugin.Calls.Count;
        scenario.Clock.Advance(TimeSpan.FromSeconds(1));
        await scenario.RunPassAsync();
        var second = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);

        first.NextAttemptAt.Should().BeCloseTo(failedAt + TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        tooSoon.Should().Be(1);
        scenario.Plugin.Calls.Should().HaveCount(2);
        second.Attempts.Should().Be(2);
        second.NextAttemptAt.Should().BeCloseTo(
            failedAt + TimeSpan.FromSeconds(30) + TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
    }

    /// <summary>After the eighth failed attempt the record is failed for good and later passes leave it alone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task EighthFailure_EndsTheRecordAndNoFurtherAttemptIsMade()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (_, _, _) => Task.FromException(new InvalidOperationException("down"));

        for (var attempt = 0; attempt < FulfilmentRecord.MaxAttempts; attempt++)
        {
            await scenario.RunPassAsync();
            scenario.Clock.Advance(FulfilmentRecord.MaxBackoff);
        }

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().HaveCount(FulfilmentRecord.MaxAttempts);
        var record = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        record.Status.Should().Be(FulfilmentStatus.Failed);
        record.Attempts.Should().Be(FulfilmentRecord.MaxAttempts);
    }

    /// <summary>A long error text is stored cut to 2000 characters, starting with what failed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task LongErrorText_IsStoredCutToTwoThousandCharacters()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (_, _, _) =>
            Task.FromException(new InvalidOperationException(new string('x', FulfilmentRecord.MaxErrorLength * 3)));

        await scenario.RunPassAsync();

        var record = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        record.LastError.Should().HaveLength(FulfilmentRecord.MaxErrorLength);
        record.LastError.Should().StartWith("InvalidOperationException: xxx");
    }

    /// <summary>A plugin that never answers fails the attempt at the call timeout instead of holding the pass.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task PluginThatNeverAnswers_FailsTheAttemptAtTheCallTimeout()
    {
        using var scenario = new FulfilmentScenario(factory, new Dictionary<string, string> { ["FulfilmentCallTimeoutSeconds"] = "1" });
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = (_, _, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);

        await scenario.RunPassAsync();

        var record = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        record.Status.Should().Be(FulfilmentStatus.Pending);
        record.LastError.Should().Contain("did not answer within 1 seconds");
    }

    /// <summary>
    /// A second dispatcher that runs while the plugin is still being called finds the record claimed and
    /// does not call the plugin again: one call per claim.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RecordBeingDelivered_IsNotDeliveredByASecondDispatcher()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Behaviour = async (_, _, _) => await scenario.RunPassAsync();

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().HaveCount(1);
        (await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid)).Status.Should().Be(FulfilmentStatus.Done);
    }

    /// <summary>A claim whose lease has not run out is left alone; once it has, the silent attempt counts as failed and the record is retried.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClaimThatWentSilent_IsCountedAsFailedOnceItsLeaseRunsOut()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        using (var scope = scenario.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = FulfilmentRecord.Create(invoiceId, scenario.PluginId, FulfilmentKind.Paid, scenario.Clock.GetUtcNow());
            record.Claim(scenario.Clock.GetUtcNow(), TimeSpan.FromMinutes(2));
            db.FulfilmentRecords.Add(record);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        scenario.Clock.Advance(TimeSpan.FromMinutes(1));
        await scenario.RunPassAsync();
        scenario.Plugin.Calls.Should().BeEmpty("the claim is still held");

        scenario.Clock.Advance(TimeSpan.FromMinutes(2));
        await scenario.RunPassAsync();
        var abandoned = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);
        scenario.Plugin.Calls.Should().BeEmpty("an abandoned attempt is recorded, not delivered");
        abandoned.Status.Should().Be(FulfilmentStatus.Pending);
        abandoned.LastError.Should().Be(FulfilmentRecord.AbandonedAttemptError);

        scenario.Clock.Advance(FulfilmentRecord.InitialBackoff);
        await scenario.RunPassAsync();
        scenario.Plugin.Calls.Should().HaveCount(1);
        (await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid)).Attempts.Should().Be(2);
    }

    /// <summary>The claim is saved with a lease of twice the call timeout, so it outlives the call.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Claim_HoldsForTwiceTheCallTimeout()
    {
        using var scenario = new FulfilmentScenario(factory, new Dictionary<string, string> { ["FulfilmentCallTimeoutSeconds"] = "45" });
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        var claimedAt = scenario.Clock.GetUtcNow();
        FulfilmentRecord? during = null;
        scenario.Plugin.Behaviour = async (_, _, _) => during = await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid);

        await scenario.RunPassAsync();

        during!.Status.Should().Be(FulfilmentStatus.Processing);
        during.NextAttemptAt.Should().BeCloseTo(claimedAt + TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(1));
    }

    /// <summary>A record whose plugin is not enabled is left untouched, with no attempt spent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RecordOfAPluginThatIsNotEnabled_IsLeftAlone()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        using (var scope = scenario.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FulfilmentRecords.Add(FulfilmentRecord.Create(
                invoiceId, $"disabled-{Guid.NewGuid():N}", FulfilmentKind.Paid, scenario.Clock.GetUtcNow()));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await scenario.RunPassAsync();

        scenario.Plugin.Calls.Should().BeEmpty();
    }

    /// <summary>A pass delivers at most the batch size, and the next pass carries on.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pass_DeliversAtMostOneBatch()
    {
        using var scenario = new FulfilmentScenario(factory, new Dictionary<string, string> { ["FulfilmentBatchSize"] = "2" });
        for (var i = 0; i < 3; i++)
        {
            await scenario.SeedRecordsAsync(await scenario.SeedInvoiceAsync(), FulfilmentKind.Paid);
        }

        await scenario.RunPassAsync();
        scenario.Plugin.Calls.Should().HaveCount(2);

        await scenario.RunPassAsync();
        scenario.Plugin.Calls.Should().HaveCount(3);
    }

    /// <summary>The client's external account is handed to the plugin.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientWithAnExternalAccount_IsHandedToThePlugin()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        using (var scope = scenario.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clientId = (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).ClientId;
            var client = await db.Clients.SingleAsync(c => c.Id == clientId);
            client.SetExternalAccount("crm", "acc-9");
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);

        await scenario.RunPassAsync();

        scenario.Plugin.LastPaid!.Account.Should().NotBeNull();
        scenario.Plugin.LastPaid.Account!.Provider.Should().Be("crm");
        scenario.Plugin.LastPaid.Account.Id.Should().Be("acc-9");
    }
}
