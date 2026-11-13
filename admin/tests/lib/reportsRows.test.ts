/**
 * Tests for the composite row keys of the report tables without a row id.
 */
import { describe, expect, it } from 'vitest'
import { renewalEmailKey, statementLineKey, vatRowKey } from '@/lib/reportsRows'
import { ReportStatementLineType } from '@/types/reportclientstatement'
import type { ReportStatementLine } from '@/types/reportclientstatement'
import { DomainReminderType } from '@/types/domain'

/** A statement entry the tests vary. */
const line: ReportStatementLine = {
  type: ReportStatementLineType.Invoice,
  date: '2026-01-02',
  invoiceId: 9,
  description: null,
  currency: 'USD',
  debit: 10,
  credit: 0,
  balance: 10,
}

describe('statementLineKey', () => {
  it('differs when the day or the running balance differs', () => {
    expect(statementLineKey(line)).not.toBe(statementLineKey({ ...line, date: '2026-01-03' }))
    expect(statementLineKey(line)).not.toBe(statementLineKey({ ...line, balance: 20 }))
    expect(statementLineKey(line)).toBe(statementLineKey({ ...line }))
  })
})

describe('renewalEmailKey', () => {
  it('names the domain, the reminder and the send time', () => {
    const row = { clientName: 'Acme', domain: 'a.test', sentAt: '2026-01-02T00:00:00Z', reminder: DomainReminderType.ExpiredNotice, recipients: 'x@y.test' }

    expect(renewalEmailKey(row)).toBe(`a.test|${row.reminder}|2026-01-02T00:00:00Z`)
  })
})

describe('vatRowKey', () => {
  it('separates the same country at two rates', () => {
    const row = { countryCode: 'DE', vatRate: 19, numberOfInvoices: 1, totalExclVat: 1, totalVatCollected: 1, currency: 'EUR' }

    expect(vatRowKey(row)).toBe('DE|19|EUR')
    expect(vatRowKey(row)).not.toBe(vatRowKey({ ...row, vatRate: 7 }))
  })
})
