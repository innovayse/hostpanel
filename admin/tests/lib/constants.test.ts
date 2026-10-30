/**
 * Tests for the shared option lists: every list offers exactly the members of the enum or const
 * object it is built from (the empty "all" entry aside), so a new member cannot be forgotten and
 * no value is a stray literal.
 */
import { describe, expect, it } from 'vitest'
import {
  BILLABLE_ITEM_TYPE_OPTIONS,
  BILLING_CYCLE_OPTIONS,
  CLIENT_STATUS_OPTIONS,
  CURRENCY_OPTIONS,
  DNS_RECORD_TYPE_OPTIONS,
  DOMAIN_STATUS_OPTIONS,
  GATEWAY_OPTIONS,
  INVOICE_STATUS_OPTIONS,
  ORDER_STATUS_OPTIONS,
  PAYMENT_METHOD_OPTIONS,
  QUOTE_STAGE_OPTIONS,
  RECURRING_PERIOD_OPTIONS,
  SERVICE_STATUS_OPTIONS,
  TICKET_PRIORITY_OPTIONS,
  TRANSACTION_METHOD_OPTIONS,
} from '@/lib/constants'
import type { SelectOption } from '@/lib/constants'
import { translate } from '@/lib/translate'
import { BillableItemType, RecurringPeriod } from '@/types/billableitem'
import { ClientStatus } from '@/types/client'
import { DnsRecordType, DomainStatus } from '@/types/domain'
import { PaymentGateway } from '@/types/gateway'
import { InvoiceStatus } from '@/types/invoice'
import { OrderStatus } from '@/types/order'
import { QuoteStage } from '@/types/quote'
import { BillingCycle, ServiceStatus } from '@/types/service'
import { TicketPriority } from '@/types/ticket'
import { PaymentMethod } from '@/types/transaction'

/**
 * The non-empty values of an option list, sorted.
 *
 * @param options - The option list.
 * @returns Its values without the empty "all" entry.
 */
const valuesOf = (options: SelectOption[]): string[] =>
  options.map((option) => option.value).filter((value) => value !== '').sort()

/**
 * The members of an enum or const object, sorted.
 *
 * @param members - The enum or const object.
 * @returns Its values.
 */
const membersOf = (members: Record<string, string>): string[] => Object.values(members).sort()

describe('option lists', () => {
  it('offer every status, stage and type exactly once', () => {
    expect(valuesOf(CLIENT_STATUS_OPTIONS)).toEqual(membersOf(ClientStatus))
    expect(valuesOf(SERVICE_STATUS_OPTIONS)).toEqual(membersOf(ServiceStatus))
    expect(valuesOf(DOMAIN_STATUS_OPTIONS)).toEqual(membersOf(DomainStatus))
    expect(valuesOf(ORDER_STATUS_OPTIONS)).toEqual(membersOf(OrderStatus))
    expect(valuesOf(QUOTE_STAGE_OPTIONS)).toEqual(membersOf(QuoteStage))
    expect(valuesOf(TICKET_PRIORITY_OPTIONS)).toEqual(membersOf(TicketPriority))
    expect(valuesOf(DNS_RECORD_TYPE_OPTIONS)).toEqual(membersOf(DnsRecordType))
    expect(valuesOf(BILLING_CYCLE_OPTIONS)).toEqual(membersOf(BillingCycle))
    expect(valuesOf(BILLABLE_ITEM_TYPE_OPTIONS)).toEqual(membersOf(BillableItemType))
    expect(valuesOf(RECURRING_PERIOD_OPTIONS)).toEqual(membersOf(RecurringPeriod))
    expect(valuesOf(GATEWAY_OPTIONS)).toEqual(membersOf(PaymentGateway))
  })

  it('offer the partially refunded invoices in the status filter, under a translated label', () => {
    const option = INVOICE_STATUS_OPTIONS.find((entry) => entry.value === InvoiceStatus.PartiallyRefunded)

    expect(option?.label).toBe(translate('clients.clientSummary.partiallyRefunded'))
    expect(option?.label).toBe('Partially Refunded')
  })

  it('draw payment methods from the payment method set', () => {
    const methods = new Set<string>(Object.values(PaymentMethod))

    for (const option of [...PAYMENT_METHOD_OPTIONS, ...TRANSACTION_METHOD_OPTIONS]) {
      expect(option.value === '' || methods.has(option.value)).toBe(true)
    }
  })

  it('price billable items in the currencies the panel knows', () => {
    expect(valuesOf(CURRENCY_OPTIONS)).toEqual(['AMD', 'EUR', 'GBP', 'USD'])
  })

  it('give every option a readable label', () => {
    const lists = [CLIENT_STATUS_OPTIONS, ORDER_STATUS_OPTIONS, QUOTE_STAGE_OPTIONS, GATEWAY_OPTIONS]

    for (const option of lists.flat()) {
      expect(option.label).not.toBe('')
      expect(option.label).not.toContain('.')
    }
  })

  it('translate the none entries instead of hard-coding English', () => {
    const noneLabel = translate('shared.none')

    expect(PAYMENT_METHOD_OPTIONS.find((option) => option.value === '')?.label).toBe(noneLabel)
    expect(GATEWAY_OPTIONS.find((option) => option.value === PaymentGateway.None)?.label).toBe(noneLabel)
    expect(noneLabel).not.toBe('shared.none')
  })
})
