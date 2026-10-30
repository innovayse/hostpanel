using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Innovayse.SDK.Plugins.Fulfilment;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Asks every enabled fulfilment plugin how much of an invoice may still be refunded, before any money
/// moves, and settles the refund at the smallest answer. The one place a refund is capped by plugins, so
/// a manual refund and the automatic refund of a failed order go through the same rule.
/// </summary>
/// <remarks>
/// <para>
/// Fail closed: a plugin that throws or does not answer within the fulfilment call timeout leaves the
/// refund refused, because an answer that could not be obtained might have been a cap. With no enabled
/// plugin there is nothing to ask and the refund is unchanged.
/// </para>
/// <para>
/// A cap of zero or less refuses the refund; a smaller positive cap reduces it and the plugin's reason is
/// written to the refund's notes, so staff can see why less came back than was asked.
/// </para>
/// </remarks>
public sealed class RefundAdviser
{
    /// <summary>The longest the notes of a refund may be, as stored on its ledger row.</summary>
    public const int NotesMaxLength = 1000;

    /// <summary>Resolves the fulfilment plugins an admin has switched on.</summary>
    private readonly IFulfilmentPluginResolver _plugins;

    /// <summary>Reads the external account a client is linked to.</summary>
    private readonly IClientExternalAccounts _accounts;

    /// <summary>The timings shared with the fulfilment dispatch: how long a plugin has to answer.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Structured logger; records a plugin that could not advise.</summary>
    private readonly ILogger<RefundAdviser> _logger;

    /// <summary>Initializes a new instance of the <see cref="RefundAdviser"/> class.</summary>
    /// <param name="plugins">Resolves the fulfilment plugins an admin has switched on.</param>
    /// <param name="accounts">Reads the external account a client is linked to.</param>
    /// <param name="options">The timings shared with the fulfilment dispatch.</param>
    /// <param name="logger">Structured logger.</param>
    public RefundAdviser(
        IFulfilmentPluginResolver plugins,
        IClientExternalAccounts accounts,
        IOptions<ScheduledJobsOptions> options,
        ILogger<RefundAdviser> logger)
    {
        _plugins = plugins;
        _accounts = accounts;
        _options = options;
        _logger = logger;
    }

    /// <summary>Asks the enabled plugins and settles what may be refunded.</summary>
    /// <param name="invoice">The paid invoice, with its items loaded.</param>
    /// <param name="requested">The amount asked for, in the invoice currency.</param>
    /// <param name="notes">The operator's notes, or <see langword="null"/>.</param>
    /// <param name="ct">The caller's cancellation token; a cancellation of it propagates.</param>
    /// <returns>
    /// The amount to refund and the notes to record with it; <c>REFUND_ADVICE_UNAVAILABLE</c> when a plugin
    /// threw or did not answer in time; <c>REFUND_BLOCKED_BY_PLUGIN</c> when the smallest cap is zero.
    /// </returns>
    public async Task<Result<RefundCap>> AdviseAsync(Invoice invoice, decimal requested, string? notes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var plugins = await _plugins.ResolveAllAsync(ct);
        if (plugins.Count == 0)
        {
            return Result<RefundCap>.Success(new RefundCap(requested, notes));
        }

        var account = await _accounts.FindAsync(invoice.ClientId, ct);
        var context = new RefundAdviceContext(
            invoice.Id, invoice.ClientId, account, FulfilmentContextBuilder.Lines(invoice), requested, invoice.Currency);

        var allowed = requested;
        var sentences = new List<string>();
        foreach (var active in plugins)
        {
            var advice = await AskAsync(active, context, invoice.Id, ct);
            if (advice is null)
            {
                return Result<RefundCap>.FailureCode(HostpanelErrorCodes.REFUND_ADVICE_UNAVAILABLE);
            }

            // A cap at or above the request caps nothing; one below it is kept, with the plugin's reason.
            var cap = Math.Max(0m, advice.MaxRefundableAmount);
            if (cap < requested)
            {
                sentences.Add(Describe(active.PluginId, cap, advice.Reason));
                allowed = Math.Min(allowed, cap);
            }
        }

        if (allowed <= 0m)
        {
            _logger.LogInformation(
                "A fulfilment plugin allows no refund of invoice {InvoiceId}: {Reasons}", invoice.Id, string.Join(" ", sentences));
            return Result<RefundCap>.FailureCode(HostpanelErrorCodes.REFUND_BLOCKED_BY_PLUGIN);
        }

        return Result<RefundCap>.Success(new RefundCap(allowed, Join(notes, sentences)));
    }

    /// <summary>Asks one plugin, giving it as long as a fulfilment delivery to answer.</summary>
    /// <param name="active">The plugin to ask.</param>
    /// <param name="context">What the plugin is told about the refund.</param>
    /// <param name="invoiceId">The invoice, for the log.</param>
    /// <param name="ct">The caller's cancellation token.</param>
    /// <returns>The advice, or <see langword="null"/> when the plugin threw or timed out.</returns>
    private async Task<RefundAdvice?> AskAsync(
        ActiveFulfilmentPlugin active, RefundAdviceContext context, int invoiceId, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Value.FulfilmentCallTimeout);
        try
        {
            return await active.Plugin.AdviseRefundAsync(context, timeout.Token);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The same failure rule as the dispatcher: only the caller's own cancellation propagates.
            _logger.LogWarning(
                ex, "Fulfilment plugin {PluginId} gave no refund advice for invoice {InvoiceId}; the refund is refused.",
                active.PluginId, invoiceId);
            return null;
        }
    }

    /// <summary>Writes the sentence that records a plugin's cap.</summary>
    /// <param name="pluginId">The plugin that capped the refund.</param>
    /// <param name="cap">The amount it allowed.</param>
    /// <param name="reason">Its reason, or <see langword="null"/>.</param>
    /// <returns>One sentence for the notes.</returns>
    private static string Describe(string pluginId, decimal cap, string? reason)
    {
        var sentence = $"Plugin '{pluginId}' capped the refund at {cap}.";
        return string.IsNullOrWhiteSpace(reason) ? sentence : $"{sentence} {reason.Trim()}";
    }

    /// <summary>Joins the operator's notes and the cap sentences, cut to the stored length.</summary>
    /// <param name="notes">The operator's notes, or <see langword="null"/>.</param>
    /// <param name="sentences">One sentence per plugin that capped the refund.</param>
    /// <returns>The notes to record, or <see langword="null"/> when there are none.</returns>
    private static string? Join(string? notes, List<string> sentences)
    {
        if (sentences.Count == 0)
        {
            return notes;
        }

        var joined = string.Join(" ", sentences.Prepend(notes).Where(s => !string.IsNullOrWhiteSpace(s)));
        return joined.Length <= NotesMaxLength ? joined : joined[..NotesMaxLength];
    }
}
