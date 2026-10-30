using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The <see cref="FulfilmentRecord"/> lifecycle: create, claim with a lease, done, failed with back-off,
/// the final failure after the last attempt, an abandoned claim, and a staff retry only from Failed.
/// </summary>
public sealed class FulfilmentRecordTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The claim lease every test dispatcher takes.</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>Builds a pending record for invoice 7, plugin <c>licences</c>, paid.</summary>
    /// <returns>A new pending record, due at <see cref="Now"/>.</returns>
    private static FulfilmentRecord NewRecord()
    {
        return FulfilmentRecord.Create(7, "licences", FulfilmentKind.Paid, Now);
    }

    /// <summary>Claims a record and fails the attempt, as many times as asked, each at the moment it is due.</summary>
    /// <param name="record">The record to run.</param>
    /// <param name="times">How many attempts to fail.</param>
    /// <returns>The moment the last attempt failed.</returns>
    private static DateTimeOffset FailAttempts(FulfilmentRecord record, int times)
    {
        var at = record.NextAttemptAt;
        for (var i = 0; i < times; i++)
        {
            at = record.NextAttemptAt;
            record.Claim(at, Lease).Should().BeTrue();
            record.MarkFailed("plugin said no", at);
        }

        return at;
    }

    /// <summary>A new record is pending, due at once, with no attempts, error or completion.</summary>
    [Fact]
    public void Create_IsPendingAndDueAtOnce()
    {
        var record = NewRecord();

        record.InvoiceId.Should().Be(7);
        record.PluginId.Should().Be("licences");
        record.Kind.Should().Be(FulfilmentKind.Paid);
        record.Status.Should().Be(FulfilmentStatus.Pending);
        record.Attempts.Should().Be(0);
        record.NextAttemptAt.Should().Be(Now);
        record.LastError.Should().BeNull();
        record.CompletedAt.Should().BeNull();
        record.IsDue(Now).Should().BeTrue();
        record.CanRetry.Should().BeFalse();
    }

    /// <summary>A record needs a plugin to tell.</summary>
    [Fact]
    public void Create_WithoutAPlugin_Throws()
    {
        var act = () => FulfilmentRecord.Create(7, " ", FulfilmentKind.Paid, Now);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A claim counts the attempt and holds the record until the lease ends; a held record is not due.</summary>
    [Fact]
    public void Claim_HoldsTheRecordForTheLease()
    {
        var record = NewRecord();

        record.Claim(Now, Lease).Should().BeTrue();

        record.Status.Should().Be(FulfilmentStatus.Processing);
        record.Attempts.Should().Be(1);
        record.NextAttemptAt.Should().Be(Now + Lease);
        record.IsDue(Now + Lease - TimeSpan.FromSeconds(1)).Should().BeFalse();
        ((Action)(() => record.Claim(Now.AddMinutes(1), Lease))).Should().Throw<InvalidOperationException>();
    }

    /// <summary>A record whose back-off is not over cannot be claimed.</summary>
    [Fact]
    public void Claim_BeforeItIsDue_Throws()
    {
        var record = FulfilmentRecord.Create(7, "licences", FulfilmentKind.Paid, Now.AddMinutes(1));

        var act = () => record.Claim(Now, Lease);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>The plugin's acceptance closes the record and clears an earlier error; nothing claims it again.</summary>
    [Fact]
    public void MarkDone_ClosesTheRecord()
    {
        var record = NewRecord();
        var failedAt = FailAttempts(record, 1);
        var retryAt = record.NextAttemptAt;
        record.Claim(retryAt, Lease);

        record.MarkDone(retryAt.AddSeconds(2));

        record.Status.Should().Be(FulfilmentStatus.Done);
        record.CompletedAt.Should().Be(retryAt.AddSeconds(2));
        record.LastError.Should().BeNull();
        record.IsDue(failedAt.AddDays(1)).Should().BeFalse();
        record.CanRetry.Should().BeFalse();
    }

    /// <summary>An outcome needs a claim: a pending record takes neither done nor failed.</summary>
    [Fact]
    public void Outcome_WithoutAClaim_Throws()
    {
        var record = NewRecord();

        ((Action)(() => record.MarkDone(Now))).Should().Throw<InvalidOperationException>();
        ((Action)(() => record.MarkFailed("x", Now))).Should().Throw<InvalidOperationException>();
    }

    /// <summary>A failed attempt with attempts left goes back to Pending, due after the back-off, with the error kept.</summary>
    [Fact]
    public void MarkFailed_WithAttemptsLeft_SchedulesTheBackoff()
    {
        var record = NewRecord();
        record.Claim(Now, Lease);

        record.MarkFailed("plugin said no", Now.AddSeconds(3));

        record.Status.Should().Be(FulfilmentStatus.Pending);
        record.LastError.Should().Be("plugin said no");
        record.NextAttemptAt.Should().Be(Now.AddSeconds(3) + TimeSpan.FromSeconds(30));
        record.CanRetry.Should().BeFalse();
    }

    /// <summary>The back-off starts at thirty seconds, doubles with each failure and stops at one hour.</summary>
    /// <param name="failedAttempts">How many attempts have failed.</param>
    /// <param name="expectedSeconds">The wait expected before the next attempt, in seconds.</param>
    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(7, 1920)]
    [InlineData(8, 3600)]
    [InlineData(200, 3600)]
    public void BackoffAfter_DoublesUpToTheCap(int failedAttempts, int expectedSeconds)
    {
        FulfilmentRecord.BackoffAfter(failedAttempts).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    /// <summary>The back-off is defined only once something has failed.</summary>
    [Fact]
    public void BackoffAfter_Zero_Throws()
    {
        var act = () => FulfilmentRecord.BackoffAfter(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>The last allowed attempt failing fails the record for good: it is not due again and staff may retry it.</summary>
    [Fact]
    public void MarkFailed_OnTheLastAttempt_FailsTheRecord()
    {
        var record = NewRecord();

        var lastFailure = FailAttempts(record, FulfilmentRecord.MaxAttempts);

        record.Attempts.Should().Be(FulfilmentRecord.MaxAttempts);
        record.Status.Should().Be(FulfilmentStatus.Failed);
        record.IsDue(lastFailure.AddDays(30)).Should().BeFalse();
        record.CanRetry.Should().BeTrue();
    }

    /// <summary>An error longer than the column holds is cut to its start.</summary>
    [Fact]
    public void MarkFailed_LongError_IsTruncated()
    {
        var record = NewRecord();
        record.Claim(Now, Lease);

        record.MarkFailed(new string('e', FulfilmentRecord.MaxErrorLength + 500), Now);

        record.LastError.Should().HaveLength(FulfilmentRecord.MaxErrorLength);
    }

    /// <summary>
    /// A claim that expired without an answer is due again, and the next claim records it as a failed attempt
    /// and schedules its back-off instead of calling the plugin at once.
    /// </summary>
    [Fact]
    public void Claim_AfterAnAbandonedAttempt_RecordsItAsFailed()
    {
        var record = NewRecord();
        record.Claim(Now, Lease);
        var expired = Now + Lease;
        record.IsDue(expired).Should().BeTrue();

        record.Claim(expired, Lease).Should().BeFalse();

        record.Status.Should().Be(FulfilmentStatus.Pending);
        record.Attempts.Should().Be(1);
        record.LastError.Should().Be(FulfilmentRecord.AbandonedAttemptError);
        record.NextAttemptAt.Should().Be(expired + FulfilmentRecord.BackoffAfter(1));
    }

    /// <summary>An abandoned last attempt fails the record for good, so a crashing plugin cannot loop forever.</summary>
    [Fact]
    public void Claim_AfterAnAbandonedLastAttempt_FailsTheRecord()
    {
        var record = NewRecord();
        FailAttempts(record, FulfilmentRecord.MaxAttempts - 1);
        var lastStart = record.NextAttemptAt;
        record.Claim(lastStart, Lease).Should().BeTrue();

        record.Claim(lastStart + Lease, Lease).Should().BeFalse();

        record.Status.Should().Be(FulfilmentStatus.Failed);
        record.LastError.Should().Be(FulfilmentRecord.AbandonedAttemptError);
    }

    /// <summary>A staff retry of a failed record makes it pending and due at once, with a fresh attempt allowance and the last error kept.</summary>
    [Fact]
    public void Retry_Failed_StartsAgain()
    {
        var record = NewRecord();
        var lastFailure = FailAttempts(record, FulfilmentRecord.MaxAttempts);
        var retryAt = lastFailure.AddHours(2);

        record.Retry(retryAt);

        record.Status.Should().Be(FulfilmentStatus.Pending);
        record.Attempts.Should().Be(0);
        record.NextAttemptAt.Should().Be(retryAt);
        record.LastError.Should().Be("plugin said no");
        record.IsDue(retryAt).Should().BeTrue();
    }

    /// <summary>A record that has not failed (pending, running or done) is refused a retry with the not-retryable code.</summary>
    [Fact]
    public void Retry_NotFailed_IsRefused()
    {
        var pending = NewRecord();
        var processing = NewRecord();
        processing.Claim(Now, Lease);
        var done = NewRecord();
        done.Claim(Now, Lease);
        done.MarkDone(Now);

        foreach (var record in new[] { pending, processing, done })
        {
            var status = record.Status;
            var act = () => record.Retry(Now);
            act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.FulfilmentRecordNotRetryable);
            record.Status.Should().Be(status);
        }
    }
}
