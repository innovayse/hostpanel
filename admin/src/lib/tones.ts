/**
 * How every closed value set the API sends (statuses, stages, types, priorities, outcomes) is
 * drawn as a kit badge: one module maps each value to a `UiTone`, so no screen owns a colour and
 * two screens cannot disagree about what "Overdue" looks like.
 *
 * The value sets themselves (`OrderStatus`, `TicketStatus` and the rest) live in `types/`. A table keyed by
 * a whole enum is exported as a `Record` (the compiler proves it is complete); a table looked up
 * with a raw API string is wrapped in a function that falls back to the neutral tone, so a value
 * the API adds later is shown as received instead of breaking the screen.
 */
import { translate, translateOrRaw } from '@/lib/translate'
import { UiTone } from '@/utils/ui/tone'
import { ClientStatus } from '@/types/client'
import { ContactType } from '@/types/contact'
import { DomainStatus } from '@/types/domain'
import { GatewayLogResult } from '@/types/gatewaylog'
import { InvoiceStatus, InvoiceTransactionType } from '@/types/invoice'
import { MigrationLogAction, MigrationStatus } from '@/types/migration'
import type { MigrationJob, MigrationLogEntry } from '@/types/migration'
import { NetworkIssuePriority, NetworkIssueStatus } from '@/types/networkissue'
import { OrderItemStatus, OrderStatus } from '@/types/order'
import { QuoteStage } from '@/types/quote'
import { CancellationStatus, CancellationType, ServiceStatus } from '@/types/service'
import { TicketPriority, TicketStatus } from '@/types/ticket'

/**
 * Looks a value up in a tone table, neutral when the API sent one we do not know.
 *
 * @param table - Tone of each known value.
 * @param value - The value as the API sent it.
 * @returns The tone to draw the value in.
 */
const toneOf = (table: Record<string, UiTone>, value: string): UiTone => table[value] ?? UiTone.Neutral

/** Badge tone of each order status. */
const ORDER_TONES: Record<OrderStatus, UiTone> = {
  [OrderStatus.Pending]: UiTone.Warning,
  [OrderStatus.Active]: UiTone.Success,
  [OrderStatus.Cancelled]: UiTone.Danger,
  [OrderStatus.Fraud]: UiTone.Danger,
}

/** Badge tone of each order item status. */
const ORDER_ITEM_TONES: Record<OrderItemStatus, UiTone> = {
  [OrderItemStatus.Pending]: UiTone.Warning,
  [OrderItemStatus.Active]: UiTone.Success,
  [OrderItemStatus.Cancelled]: UiTone.Danger,
}

/** Badge tone of each service status. */
const SERVICE_TONES: Record<ServiceStatus, UiTone> = {
  [ServiceStatus.Active]: UiTone.Success,
  [ServiceStatus.Pending]: UiTone.Primary,
  [ServiceStatus.Suspended]: UiTone.Warning,
  [ServiceStatus.Terminated]: UiTone.Neutral,
}

/** Badge tone of each domain status. */
const DOMAIN_TONES: Record<DomainStatus, UiTone> = {
  [DomainStatus.Active]: UiTone.Success,
  [DomainStatus.PendingRegistration]: UiTone.Primary,
  [DomainStatus.PendingTransfer]: UiTone.Primary,
  [DomainStatus.Expired]: UiTone.Danger,
  [DomainStatus.Redemption]: UiTone.Warning,
  [DomainStatus.Transferred]: UiTone.Neutral,
  [DomainStatus.Cancelled]: UiTone.Neutral,
}

/** Badge tone of each invoice status. */
const INVOICE_TONES: Record<InvoiceStatus, UiTone> = {
  [InvoiceStatus.Draft]: UiTone.Primary,
  [InvoiceStatus.Unpaid]: UiTone.Warning,
  [InvoiceStatus.Paid]: UiTone.Success,
  [InvoiceStatus.Overdue]: UiTone.Danger,
  [InvoiceStatus.Cancelled]: UiTone.Neutral,
  [InvoiceStatus.Refunded]: UiTone.Info,
  [InvoiceStatus.PartiallyRefunded]: UiTone.Info,
  [InvoiceStatus.Collections]: UiTone.Danger,
  [InvoiceStatus.PaymentPending]: UiTone.Warning,
}

/** Badge tone of each client status. */
const CLIENT_STATUS_TONES: Record<ClientStatus, UiTone> = {
  [ClientStatus.Active]: UiTone.Success,
  [ClientStatus.Suspended]: UiTone.Warning,
  [ClientStatus.Closed]: UiTone.Danger,
  [ClientStatus.Inactive]: UiTone.Neutral,
}

/** Badge tone of each contact type. */
const CONTACT_TYPE_TONES: Record<ContactType, UiTone> = {
  [ContactType.Billing]: UiTone.Primary,
  [ContactType.Technical]: UiTone.Warning,
  [ContactType.General]: UiTone.Neutral,
}

/** Badge tone of each quote stage. */
const QUOTE_STAGE_TONES: Record<QuoteStage, UiTone> = {
  [QuoteStage.Draft]: UiTone.Neutral,
  [QuoteStage.Delivered]: UiTone.Primary,
  [QuoteStage.OnHold]: UiTone.Warning,
  [QuoteStage.Accepted]: UiTone.Success,
  [QuoteStage.Lost]: UiTone.Danger,
  [QuoteStage.Expired]: UiTone.Warning,
  [QuoteStage.Dead]: UiTone.Neutral,
}

/** Badge tone of each invoice transaction type. */
const TRANSACTION_TYPE_TONES: Record<InvoiceTransactionType, UiTone> = {
  [InvoiceTransactionType.Payment]: UiTone.Success,
  [InvoiceTransactionType.Refund]: UiTone.Danger,
  [InvoiceTransactionType.Credit]: UiTone.Warning,
}

/** Badge tone of the gateway log outcomes that are drawn in colour; the rest read neutral. */
const GATEWAY_LOG_RESULT_TONES: Record<string, UiTone> = {
  [GatewayLogResult.Payment]: UiTone.Success,
  [GatewayLogResult.Refund]: UiTone.Danger,
  [GatewayLogResult.Credit]: UiTone.Warning,
  [GatewayLogResult.Pending]: UiTone.Info,
}

/**
 * Tone per status word the report endpoints send. The words span several lifecycles (orders,
 * services, invoices, transactions), so the keys are plain words, not one enum.
 */
const REPORT_STATUS_TONES: Record<string, UiTone> = {
  Active: UiTone.Success,
  Paid: UiTone.Success,
  Completed: UiTone.Success,
  Pending: UiTone.Warning,
  Invoice: UiTone.Warning,
  Payment: UiTone.Success,
  Unpaid: UiTone.Warning,
  Suspended: UiTone.Danger,
  Overdue: UiTone.Danger,
  Expired: UiTone.Danger,
  Refunded: UiTone.Info,
  PartiallyRefunded: UiTone.Info,
  Transferred: UiTone.Info,
  Draft: UiTone.Info,
  Inactive: UiTone.Neutral,
  Cancelled: UiTone.Neutral,
  Terminated: UiTone.Neutral,
}

/**
 * Tone of an order status badge.
 *
 * @param status - Order status as sent by the API.
 * @returns The kit tone.
 */
export const orderStatusTone = (status: OrderStatus): UiTone => toneOf(ORDER_TONES, status)

/**
 * Tone of an order item status badge.
 *
 * @param status - Order item status as sent by the API.
 * @returns The kit tone.
 */
export const orderItemStatusTone = (status: OrderItemStatus): UiTone => toneOf(ORDER_ITEM_TONES, status)

/**
 * Tone of a service status badge.
 *
 * @param status - Service status as sent by the API.
 * @returns The kit tone.
 */
export const serviceStatusTone = (status: ServiceStatus): UiTone => toneOf(SERVICE_TONES, status)

/**
 * Tone of a domain status badge.
 *
 * @param status - Domain status as sent by the API.
 * @returns The kit tone.
 */
export const domainStatusTone = (status: string): UiTone => toneOf(DOMAIN_TONES, status)

/**
 * Tone of an invoice status badge.
 *
 * @param status - Invoice status as sent by the API.
 * @returns The kit tone.
 */
export const invoiceStatusTone = (status: string): UiTone => toneOf(INVOICE_TONES, status)

/**
 * Tone of a client status badge.
 *
 * @param status - The status string the API sent.
 * @returns The tone; neutral for a status this panel does not know.
 */
export const clientStatusTone = (status: string): UiTone => toneOf(CLIENT_STATUS_TONES, status)

/**
 * Tone of a contact type badge.
 *
 * @param type - The contact type string the API sent.
 * @returns The tone; neutral for a type this panel does not know.
 */
export const contactTypeTone = (type: string): UiTone => toneOf(CONTACT_TYPE_TONES, type)

/**
 * Tone of a quote stage badge.
 *
 * @param stage - The stage string the API sent.
 * @returns The tone; neutral for a stage this panel does not know.
 */
export const quoteStageTone = (stage: string): UiTone => toneOf(QUOTE_STAGE_TONES, stage)

/**
 * Tone of an invoice transaction type badge.
 *
 * @param type - The transaction type string the API sent.
 * @returns The tone; neutral for a type this panel does not know.
 */
export const transactionTypeTone = (type: string): UiTone => toneOf(TRANSACTION_TYPE_TONES, type)

/**
 * Tone of a gateway log outcome badge.
 *
 * @param result - The result string the log carries.
 * @returns The tone; neutral for an outcome this panel does not know.
 */
export const gatewayLogResultTone = (result: string): UiTone => toneOf(GATEWAY_LOG_RESULT_TONES, result)

/**
 * Tone of a status word a report endpoint sent.
 *
 * @param status - Status as the endpoint sent it.
 * @returns The tone for the badge; neutral for an unknown word.
 */
export const reportStatusTone = (status: string): UiTone => toneOf(REPORT_STATUS_TONES, status)

/** Badge tone of each ticket status. */
export const ticketStatusTone: Record<TicketStatus, UiTone> = {
  [TicketStatus.Open]: UiTone.Primary,
  [TicketStatus.AwaitingReply]: UiTone.Warning,
  [TicketStatus.Answered]: UiTone.Success,
  [TicketStatus.Closed]: UiTone.Neutral,
}

/** Badge tone of each ticket priority. */
export const ticketPriorityTone: Record<TicketPriority, UiTone> = {
  [TicketPriority.Low]: UiTone.Neutral,
  [TicketPriority.Medium]: UiTone.Warning,
  [TicketPriority.High]: UiTone.Danger,
}

/** Badge tone of each network issue priority. */
export const networkIssuePriorityTone: Record<NetworkIssuePriority, UiTone> = {
  [NetworkIssuePriority.Low]: UiTone.Neutral,
  [NetworkIssuePriority.Medium]: UiTone.Warning,
  [NetworkIssuePriority.High]: UiTone.Warning,
  [NetworkIssuePriority.Critical]: UiTone.Danger,
}

/** Badge tone of each network issue status. */
export const networkIssueStatusTone: Record<NetworkIssueStatus, UiTone> = {
  [NetworkIssueStatus.Reported]: UiTone.Warning,
  [NetworkIssueStatus.Investigating]: UiTone.Warning,
  [NetworkIssueStatus.Scheduled]: UiTone.Primary,
  [NetworkIssueStatus.Resolved]: UiTone.Success,
}

/** Badge tone per migration job state. */
export const migrationStatusTone: Record<MigrationJob['status'], UiTone> = {
  [MigrationStatus.Pending]: UiTone.Warning,
  [MigrationStatus.InProgress]: UiTone.Primary,
  [MigrationStatus.Completed]: UiTone.Success,
  [MigrationStatus.Failed]: UiTone.Danger,
  [MigrationStatus.Queued]: UiTone.Primary,
}

/** Badge tone per migration import-log outcome. */
export const migrationActionTone: Record<MigrationLogEntry['action'], UiTone> = {
  [MigrationLogAction.Imported]: UiTone.Success,
  [MigrationLogAction.Skipped]: UiTone.Warning,
  [MigrationLogAction.Failed]: UiTone.Danger,
}

/** Badge tone of each cancellation type. */
const CANCELLATION_TYPE_TONES: Record<CancellationType, UiTone> = {
  [CancellationType.Immediate]: UiTone.Danger,
  [CancellationType.EndOfBillingPeriod]: UiTone.Primary,
}

/** Badge tone of each cancellation request status. */
const CANCELLATION_STATUS_TONES: Record<CancellationStatus, UiTone> = {
  [CancellationStatus.Open]: UiTone.Success,
  [CancellationStatus.Closed]: UiTone.Neutral,
}

/**
 * Tone of a cancellation type badge.
 *
 * @param type - Cancellation type as sent by the API.
 * @returns The kit tone.
 */
export const cancellationTypeTone = (type: CancellationType): UiTone => toneOf(CANCELLATION_TYPE_TONES, type)

/**
 * Tone of a cancellation request status badge.
 *
 * @param status - Cancellation status as sent by the API.
 * @returns The kit tone.
 */
export const cancellationStatusTone = (status: CancellationStatus): UiTone => toneOf(CANCELLATION_STATUS_TONES, status)

/**
 * Translated name of a cancellation type; a type the API adds later is shown as received.
 *
 * @param type - Cancellation type as sent by the API.
 * @returns The label to show.
 */
export const cancellationTypeLabel = (type: CancellationType): string => {
  switch (type) {
    case CancellationType.Immediate:
      return translate('cancellationTypes.immediate')
    case CancellationType.EndOfBillingPeriod:
      return translate('cancellationTypes.endOfBillingPeriod')
    default:
      return type
  }
}

/**
 * Translated name of a cancellation request status; a status the API adds later is shown as received.
 *
 * @param status - Cancellation status as sent by the API.
 * @returns The label to show.
 */
export const cancellationStatusLabel = (status: CancellationStatus): string => {
  switch (status) {
    case CancellationStatus.Open:
      return translate('cancellationTypes.statusOpen')
    case CancellationStatus.Closed:
      return translate('cancellationTypes.statusClosed')
    default:
      return status
  }
}

/**
 * Translated name of a status, taken from the option list a filter already uses.
 *
 * @param options - Option list whose `value` is the status string and `label` its translation.
 * @param status - Status as sent by the API.
 * @returns The translated label, or the status itself when the list does not know it.
 */
export const statusLabel = (options: ReadonlyArray<{ value: string; label: string }>, status: string): string =>
  options.find((option) => option.value === status)?.label ?? status

/**
 * Display label of a ticket status: the translation when the catalogue has one, the raw status
 * otherwise, so a status the API adds later is shown as received.
 *
 * @param status - The ticket status string.
 * @returns The label to show.
 */
export const ticketStatusLabel = (status: string): string => translateOrRaw(`ticketStatus.${status}`, status)
