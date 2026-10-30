/**
 * Shared option lists for the admin screens: every dropdown and radio group whose values are a
 * closed set the API defines. Each `value` is a member of the enum or const object in `types/`,
 * never a repeated literal. Badge colours live in `tones.ts`, not here.
 *
 * Option labels are getters so they resolve in the operator's current language each time a
 * dropdown reads them, rather than freezing the language that was active at import time.
 */

import { translate } from '@/lib/translate'
import { MessageLanguage } from '@/locales/messages'
import { ClientStatus } from '@/types/client'
import { CurrencyCode } from '@/types/currency'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { DnsRecordType, DomainStatus } from '@/types/domain'
import { PaymentGateway } from '@/types/gateway'
import { InvoiceStatus } from '@/types/invoice'
import { OrderStatus } from '@/types/order'
import { QuoteStage } from '@/types/quote'
import { BillingCycle, ServiceStatus } from '@/types/service'
import { TicketPriority } from '@/types/ticket'
import { PaymentMethod } from '@/types/transaction'

/**
 * One option of a dropdown or radio group: the wire value and the label shown for it.
 *
 * @typeParam T - The wire value type; an enum when the options cover a closed set.
 */
export interface SelectOption<T extends string = string> {
  /** Value stored and sent to the API. */
  value: T
  /** Text shown to the operator, in the current language. */
  label: string
}

/** Client account status options for dropdowns. */
export const CLIENT_STATUS_OPTIONS: SelectOption<ClientStatus>[] = [
  { value: ClientStatus.Active, get label() { return translate('shared.active') } },
  { value: ClientStatus.Inactive, get label() { return translate('shared.inactive') } },
  { value: ClientStatus.Suspended, get label() { return translate('clients.clientForm.suspended') } },
  { value: ClientStatus.Closed, get label() { return translate('clients.clientForm.closed') } },
]

/** Service status options for dropdowns. */
export const SERVICE_STATUS_OPTIONS: SelectOption<ServiceStatus>[] = [
  { value: ServiceStatus.Pending, get label() { return translate('constants.serviceStatusOptions.pending') } },
  { value: ServiceStatus.Active, get label() { return translate('shared.active') } },
  { value: ServiceStatus.Suspended, get label() { return translate('clients.clientForm.suspended') } },
  { value: ServiceStatus.Terminated, get label() { return translate('constants.serviceStatusOptions.terminated') } },
]

/** Language options for dropdowns. */
export const LANGUAGE_OPTIONS: SelectOption[] = [
  { value: '', get label() { return translate('shared.default') } },
  { value: MessageLanguage.En, get label() { return translate('clients.clientForm.english') } },
  { value: MessageLanguage.Ru, get label() { return translate('clients.clientForm.russian') } },
  { value: MessageLanguage.Hy, get label() { return translate('clients.clientForm.armenian') } },
]

/** Domain status options for dropdowns. */
export const DOMAIN_STATUS_OPTIONS: SelectOption[] = [
  { value: DomainStatus.Active, get label() { return translate('shared.active') } },
  { value: DomainStatus.PendingRegistration, get label() { return translate('constants.domainStatusOptions.pendingRegistration') } },
  { value: DomainStatus.PendingTransfer, get label() { return translate('constants.domainStatusOptions.pendingTransfer') } },
  { value: DomainStatus.Expired, get label() { return translate('billing.quotesList.expired') } },
  { value: DomainStatus.Redemption, get label() { return translate('constants.domainStatusOptions.redemption') } },
  { value: DomainStatus.Transferred, get label() { return translate('constants.domainStatusOptions.transferred') } },
  { value: DomainStatus.Cancelled, get label() { return translate('clients.clientSummary.cancelled') } },
]

/** Payment method options for dropdowns (the empty entry means "none"). */
export const PAYMENT_METHOD_OPTIONS: SelectOption[] = [
  { value: '', get label() { return translate('shared.none') } },
  { value: PaymentMethod.Card, get label() { return translate('billing.addTransaction.creditDebitCard') } },
  { value: PaymentMethod.BankTransfer, get label() { return translate('constants.paymentMethodOptions.bankTransfer') } },
  { value: PaymentMethod.PayPal, label: 'PayPal' },
]

/** Payment methods a ledger transaction can name (the empty "none" entry is the caller's). */
export const TRANSACTION_METHOD_OPTIONS: SelectOption[] = [
  { value: PaymentMethod.Stripe, label: 'Stripe' },
  { value: PaymentMethod.PayPal, label: 'PayPal' },
  { value: PaymentMethod.Card, get label() { return translate('billing.addTransaction.creditDebitCard') } },
]

/** Currencies a billable item can be priced in. */
export const CURRENCY_OPTIONS: SelectOption[] = [
  { value: CurrencyCode.Usd, label: 'USD' },
  { value: CurrencyCode.Eur, label: 'EUR' },
  { value: CurrencyCode.Gbp, label: 'GBP' },
  { value: CurrencyCode.Amd, label: 'AMD' },
]

/** DNS record type options for dropdowns. */
export const DNS_RECORD_TYPE_OPTIONS: SelectOption[] = [
  { value: DnsRecordType.A, label: 'A' },
  { value: DnsRecordType.Aaaa, label: 'AAAA' },
  { value: DnsRecordType.Cname, label: 'CNAME' },
  { value: DnsRecordType.Mx, label: 'MX' },
  { value: DnsRecordType.Txt, label: 'TXT' },
  { value: DnsRecordType.Ns, label: 'NS' },
  { value: DnsRecordType.Srv, label: 'SRV' },
]

/** Billing cycle options for service dropdowns. */
export const BILLING_CYCLE_OPTIONS: SelectOption[] = [
  { value: BillingCycle.Monthly, get label() { return translate('settings.productForm.monthly') } },
  { value: BillingCycle.Annual, get label() { return translate('settings.productForm.annual') } },
]

/** Billable item type options: a one time charge or a recurring one. */
export const BILLABLE_ITEM_TYPE_OPTIONS: SelectOption<BillableItemType>[] = [
  { value: BillableItemType.OneTime, get label() { return translate('billing.billableItems.typeOneTime') } },
  { value: BillableItemType.Recurring, get label() { return translate('billing.billableItems.typeRecurring') } },
]

/** How often a recurring billable item repeats. */
export const RECURRING_PERIOD_OPTIONS: SelectOption<RecurringPeriod>[] = [
  { value: RecurringPeriod.Weekly, get label() { return translate('constants.recurringPeriodOptions.weekly') } },
  { value: RecurringPeriod.Monthly, get label() { return translate('constants.recurringPeriodOptions.monthly') } },
  { value: RecurringPeriod.Quarterly, get label() { return translate('constants.recurringPeriodOptions.quarterly') } },
  { value: RecurringPeriod.Annually, get label() { return translate('constants.recurringPeriodOptions.annually') } },
]

/** Invoice status options for filter dropdowns. */
export const INVOICE_STATUS_OPTIONS: SelectOption[] = [
  { value: '', get label() { return translate('clients.clientInvoices.allStatuses') } },
  { value: InvoiceStatus.Draft, get label() { return translate('clients.clientSummary.draft') } },
  { value: InvoiceStatus.Unpaid, get label() { return translate('billing.invoiceDetail.unpaid') } },
  { value: InvoiceStatus.Paid, get label() { return translate('clients.clientSummary.paid') } },
  { value: InvoiceStatus.Overdue, get label() { return translate('clients.clientSummary.overdue') } },
  { value: InvoiceStatus.Cancelled, get label() { return translate('clients.clientSummary.cancelled') } },
  { value: InvoiceStatus.PartiallyRefunded, get label() { return translate('clients.clientSummary.partiallyRefunded') } },
  { value: InvoiceStatus.Refunded, get label() { return translate('clients.clientSummary.refunded') } },
]

/** Quote stage options for filter dropdowns. */
export const QUOTE_STAGE_OPTIONS: SelectOption[] = [
  { value: '', get label() { return translate('constants.quoteStageOptions.allStages') } },
  { value: QuoteStage.Draft, get label() { return translate('clients.clientSummary.draft') } },
  { value: QuoteStage.Delivered, get label() { return translate('billing.addQuote.delivered') } },
  { value: QuoteStage.OnHold, get label() { return translate('billing.addQuote.onHold') } },
  { value: QuoteStage.Accepted, get label() { return translate('billing.addQuote.accepted') } },
  { value: QuoteStage.Lost, get label() { return translate('billing.addQuote.lost') } },
  { value: QuoteStage.Expired, get label() { return translate('billing.quotesList.expired') } },
  { value: QuoteStage.Dead, get label() { return translate('billing.addQuote.dead') } },
]

/** Payment gateway options. */
export const GATEWAY_OPTIONS: SelectOption[] = [
  { value: PaymentGateway.None, get label() { return translate('shared.none') } },
  { value: PaymentGateway.BankTransfer, get label() { return translate('constants.paymentMethodOptions.bankTransfer') } },
  { value: PaymentGateway.Stripe, label: 'Stripe' },
  { value: PaymentGateway.PayPal, label: 'PayPal' },
]

/** Order status options for filter dropdowns. */
export const ORDER_STATUS_OPTIONS: SelectOption[] = [
  { value: '', get label() { return translate('clients.clientInvoices.allStatuses') } },
  { value: OrderStatus.Pending, get label() { return translate('constants.serviceStatusOptions.pending') } },
  { value: OrderStatus.Active, get label() { return translate('shared.active') } },
  { value: OrderStatus.Cancelled, get label() { return translate('clients.clientSummary.cancelled') } },
  { value: OrderStatus.Fraud, get label() { return translate('constants.orderStatusOptions.fraud') } },
]

/** Available ticket priority options for dropdowns. */
export const TICKET_PRIORITY_OPTIONS = [
  { value: TicketPriority.Low, get label() { return translate('support.networkIssueForm.low') } },
  { value: TicketPriority.Medium, get label() { return translate('support.networkIssueForm.medium') } },
  { value: TicketPriority.High, get label() { return translate('support.networkIssueForm.high') } },
]
