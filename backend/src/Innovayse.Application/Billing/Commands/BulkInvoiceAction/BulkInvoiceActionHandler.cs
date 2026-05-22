namespace Innovayse.Application.Billing.Commands.BulkInvoiceAction;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Performs a bulk action on multiple invoices and returns the count of affected invoices.</summary>
public sealed class BulkInvoiceActionHandler(IInvoiceRepository repo, IUnitOfWork uow)
{
    /// <summary>Supported bulk action: mark invoices as paid.</summary>
    private const string ActionMarkPaid = "MarkPaid";

    /// <summary>Supported bulk action: mark invoices as unpaid.</summary>
    private const string ActionMarkUnpaid = "MarkUnpaid";

    /// <summary>Supported bulk action: cancel invoices.</summary>
    private const string ActionMarkCancelled = "MarkCancelled";

    /// <summary>Supported bulk action: duplicate invoices.</summary>
    private const string ActionDuplicate = "Duplicate";

    /// <summary>Supported bulk action: delete invoices.</summary>
    private const string ActionDelete = "Delete";

    /// <summary>
    /// Handles <see cref="BulkInvoiceActionCommand"/>.
    /// </summary>
    /// <param name="cmd">The bulk action command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of invoices affected by the action.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the action is not recognised.</exception>
    public async Task<int> HandleAsync(BulkInvoiceActionCommand cmd, CancellationToken ct)
    {
        var invoices = await repo.FindByIdsAsync(cmd.InvoiceIds, ct);
        var affected = 0;

        foreach (var invoice in invoices)
        {
            switch (cmd.Action)
            {
                case ActionMarkPaid:
                    if (invoice.Status is InvoiceStatus.Unpaid or InvoiceStatus.Overdue)
                    {
                        var txId = $"bulk-{invoice.Id}-{DateTimeOffset.UtcNow.Ticks}";
                        invoice.MarkPaid(txId);
                        affected++;
                    }
                    break;

                case ActionMarkUnpaid:
                    if (invoice.Status == InvoiceStatus.Paid)
                    {
                        invoice.MarkUnpaid();
                        affected++;
                    }
                    break;

                case ActionMarkCancelled:
                    if (invoice.Status is not (InvoiceStatus.Paid or InvoiceStatus.Refunded))
                    {
                        invoice.Cancel();
                        affected++;
                    }
                    break;

                case ActionDuplicate:
                    var copy = invoice.Duplicate();
                    repo.Add(copy);
                    affected++;
                    break;

                case ActionDelete:
                    if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
                    {
                        repo.Remove(invoice);
                        affected++;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown bulk action: {cmd.Action}.");
            }
        }

        await uow.SaveChangesAsync(ct);
        return affected;
    }
}
