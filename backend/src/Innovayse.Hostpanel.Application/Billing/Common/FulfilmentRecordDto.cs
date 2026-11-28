using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One fulfilment record of an invoice as staff see it: which plugin was told what, and how far the
/// delivery got. Staff only: <paramref name="LastErrorMessage"/> says what went wrong and is not for a client route.
/// </summary>
/// <param name="Id">The record id.</param>
/// <param name="InvoiceId">The invoice the plugin is told about.</param>
/// <param name="PluginId">The fulfilment plugin to tell, as the plugin declares it.</param>
/// <param name="Kind">What happened to the invoice that the plugin is told about.</param>
/// <param name="Status">Where the delivery stands.</param>
/// <param name="Attempts">How many attempts have been started since the record was created or last retried.</param>
/// <param name="NextAttemptAt">When the record is next due (UTC); not meaningful once it is Done or Failed.</param>
/// <param name="LastErrorMessage">
/// The sentence for the latest failed attempt, in the caller's language, or <see langword="null"/> when none failed.
/// </param>
/// <param name="CompletedAt">When the plugin accepted the delivery (UTC), or <see langword="null"/> until it has.</param>
/// <param name="CreatedAt">When the record was written (UTC).</param>
/// <param name="CanRetry">Whether staff may send the record again: only once every attempt failed.</param>
public sealed record FulfilmentRecordDto(
    int Id,
    int InvoiceId,
    string PluginId,
    FulfilmentKind Kind,
    FulfilmentStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastErrorMessage,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    bool CanRetry);
