using System.Linq.Expressions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// One fulfilment plugin to be told one thing about one invoice (paid, refunded or lapsed), and how far
/// that delivery has got. Written in the same save as the payment, refund or lapse it reports, so whether
/// a plugin hears about it never depends on the process surviving after the save.
/// Stored in the <c>FulfilmentRecords</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at least once: a dispatcher <see cref="Claim"/>s a due record for a lease, calls the plugin,
/// then records <see cref="MarkDone"/> or <see cref="MarkFailed"/>. A dispatcher that dies in between never
/// answers; once the lease has run out the record is due again and the next claim counts the silent attempt
/// as a failure. Plugins are therefore idempotent on the invoice id.
/// </para>
/// <para>
/// <see cref="NextAttemptAt"/> is both the back-off and the lease: while <see cref="FulfilmentStatus.Pending"/>
/// it is when the next attempt may start, while <see cref="FulfilmentStatus.Processing"/> it is when the claim
/// expires. One column answers "which records are due" for both, which is what <see cref="DueFilter"/> asks.
/// </para>
/// </remarks>
public sealed class FulfilmentRecord : BaseEntity, IAggregateRoot
{
    /// <summary>How many attempts a record gets before it fails for good and waits for staff.</summary>
    public const int MaxAttempts = 8;

    /// <summary>
    /// The longest an error text kept in <see cref="LastError"/> may be, in characters. A plugin's exception
    /// text can be any size; the column holds the start of it, enough for staff to see what went wrong.
    /// </summary>
    public const int MaxErrorLength = 2000;

    /// <summary>
    /// The error recorded for an attempt whose dispatcher never answered before its lease ran out: the
    /// process stopped or lost the database between claiming the record and saving the outcome.
    /// </summary>
    public const string AbandonedAttemptError = "The attempt was abandoned before it reported an outcome.";

    /// <summary>The wait after the first failed attempt; each further failure doubles it.</summary>
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(30);

    /// <summary>The longest wait between two attempts, however many have failed.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    /// <summary>Gets the id of the invoice the plugin is told about.</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets the id of the fulfilment plugin to tell, as the plugin declares it.</summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>Gets what happened to the invoice that the plugin is told about.</summary>
    public FulfilmentKind Kind { get; private set; }

    /// <summary>Gets where the delivery stands.</summary>
    public FulfilmentStatus Status { get; private set; }

    /// <summary>Gets how many attempts have been started since the record was created or last retried.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Gets when the record is next due (UTC): the earliest start of the next attempt while Pending, the
    /// end of the current claim while Processing. Not read once the record is Done or Failed.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>
    /// Gets the error of the latest failed attempt, at most <see cref="MaxErrorLength"/> characters, or
    /// <see langword="null"/> when none failed or the delivery succeeded. Staff only: never shown to a client.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>Gets when the plugin accepted the delivery (UTC), or <see langword="null"/> until it has.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Gets when the record was written (UTC); stamped on insert by the persistence interceptor.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets whether staff may send the record again with <see cref="Retry"/>: only once every attempt failed.
    /// A pending or running record is already on its way, and a done one has nothing left to deliver.
    /// </summary>
    public bool CanRetry => Status == FulfilmentStatus.Failed;

    /// <summary>
    /// Gets the filter for the records a dispatcher may claim at <paramref name="now"/>: pending ones whose
    /// back-off is over, and running ones whose claim has expired. The query form of <see cref="IsDue"/>, so a
    /// database read and the entity never disagree on which records are due.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns>The filter expression.</returns>
    public static Expression<Func<FulfilmentRecord, bool>> DueFilter(DateTimeOffset now) =>
        r => (r.Status == FulfilmentStatus.Pending || r.Status == FulfilmentStatus.Processing) && r.NextAttemptAt <= now;

    /// <summary>
    /// Gets whether a dispatcher may <see cref="Claim"/> the record at <paramref name="now"/>: it is pending and
    /// its back-off is over, or it is running and its claim has expired. The same rule as <see cref="DueFilter"/>.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns><see langword="true"/> when the record is due.</returns>
    public bool IsDue(DateTimeOffset now) =>
        (Status is FulfilmentStatus.Pending or FulfilmentStatus.Processing) && NextAttemptAt <= now;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private FulfilmentRecord() { }

    /// <summary>Creates a pending record, due at once.</summary>
    /// <param name="invoiceId">The invoice the plugin is told about.</param>
    /// <param name="pluginId">The fulfilment plugin to tell.</param>
    /// <param name="kind">What happened to the invoice.</param>
    /// <param name="now">The current moment (UTC); the record is due from it.</param>
    /// <returns>A new, unsaved <see cref="FulfilmentRecord"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> is empty or white space.</exception>
    public static FulfilmentRecord Create(int invoiceId, string pluginId, FulfilmentKind kind, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return new FulfilmentRecord
        {
            InvoiceId = invoiceId,
            PluginId = pluginId,
            Kind = kind,
            Status = FulfilmentStatus.Pending,
            NextAttemptAt = now,
        };
    }

    /// <summary>
    /// How long to wait after a number of failed attempts: <see cref="InitialBackoff"/> after the first,
    /// doubling with each further one, never more than <see cref="MaxBackoff"/>.
    /// </summary>
    /// <param name="failedAttempts">How many attempts have failed so far; at least one.</param>
    /// <returns>The wait before the next attempt.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failedAttempts"/> is less than one.</exception>
    public static TimeSpan BackoffAfter(int failedAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);

        // Compared before multiplying: doubling thirty seconds overflows a TimeSpan long before the cap
        // would stop it, so the doubling stops as soon as it reaches the cap.
        var wait = InitialBackoff;
        for (var i = 1; i < failedAttempts && wait < MaxBackoff; i++)
        {
            wait *= 2;
        }

        return wait < MaxBackoff ? wait : MaxBackoff;
    }

    /// <summary>
    /// Claims a due record for one attempt: it turns Processing until <paramref name="now"/> plus
    /// <paramref name="lease"/>, and the attempt is counted. A record whose previous claim expired without an
    /// answer is not claimed: that silent attempt is recorded as failed first (<see cref="AbandonedAttemptError"/>),
    /// which schedules its back-off or, after the last attempt, fails it for good.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="lease">How long the claim holds before another dispatcher may take the record over.</param>
    /// <returns>
    /// <see langword="true"/> when the caller now holds the record and calls the plugin; <see langword="false"/>
    /// when an abandoned attempt was recorded instead, which the caller saves without calling the plugin.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lease"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">The record is not due (<see cref="IsDue"/>).</exception>
    public bool Claim(DateTimeOffset now, TimeSpan lease)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        if (!IsDue(now))
        {
            throw new InvalidOperationException($"A fulfilment record in status {Status} is not due at {now:O}.");
        }

        if (Status == FulfilmentStatus.Processing)
        {
            RecordFailure(AbandonedAttemptError, now);
            return false;
        }

        Status = FulfilmentStatus.Processing;
        Attempts++;
        NextAttemptAt = now + lease;
        return true;
    }

    /// <summary>Records that the plugin accepted the delivery. Nothing happens to the record after this.</summary>
    /// <param name="now">When the plugin answered (UTC).</param>
    /// <exception cref="InvalidOperationException">The record is not claimed (Processing).</exception>
    public void MarkDone(DateTimeOffset now)
    {
        EnsureProcessing();

        Status = FulfilmentStatus.Done;
        CompletedAt = now;
        LastError = null;
    }

    /// <summary>
    /// Records that the claimed attempt failed. While attempts remain the record goes back to Pending and is
    /// due again after <see cref="BackoffAfter"/>; after the <see cref="MaxAttempts"/>-th it is Failed and waits
    /// for staff to <see cref="Retry"/> it.
    /// </summary>
    /// <param name="error">What went wrong, for staff; cut to <see cref="MaxErrorLength"/> characters.</param>
    /// <param name="now">When the attempt failed (UTC).</param>
    /// <exception cref="InvalidOperationException">The record is not claimed (Processing).</exception>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        EnsureProcessing();
        RecordFailure(error, now);
    }

    /// <summary>
    /// Sends a failed record again on a staff request: it is pending and due at once, with a fresh allowance
    /// of <see cref="MaxAttempts"/> attempts. <see cref="LastError"/> stays until the next attempt answers, so
    /// staff still see why it failed.
    /// </summary>
    /// <param name="now">The current moment (UTC); the record is due from it.</param>
    /// <exception cref="DomainRuleException">
    /// <see cref="BillingErrors.FulfilmentRecordNotRetryable"/> when the record has not failed (<see cref="CanRetry"/>).
    /// </exception>
    public void Retry(DateTimeOffset now)
    {
        if (!CanRetry)
        {
            throw new DomainRuleException(
                BillingErrors.FulfilmentRecordNotRetryable,
                $"Only a Failed fulfilment record can be retried; current status is {Status}.");
        }

        Status = FulfilmentStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
    }

    /// <summary>
    /// Records a failed attempt: the error, then either the back-off before the next attempt or, when this was
    /// the last allowed one, the final failure. Status and due time change together here only.
    /// </summary>
    /// <param name="error">What went wrong; cut to <see cref="MaxErrorLength"/> characters.</param>
    /// <param name="now">When the attempt failed (UTC).</param>
    private void RecordFailure(string error, DateTimeOffset now)
    {
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        if (Attempts >= MaxAttempts)
        {
            Status = FulfilmentStatus.Failed;
            return;
        }

        Status = FulfilmentStatus.Pending;
        NextAttemptAt = now + BackoffAfter(Attempts);
    }

    /// <summary>Refuses an outcome for a record no dispatcher holds.</summary>
    /// <exception cref="InvalidOperationException">The record is not Processing.</exception>
    private void EnsureProcessing()
    {
        if (Status != FulfilmentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Only a claimed (Processing) fulfilment record can take an outcome; current status is {Status}.");
        }
    }
}
