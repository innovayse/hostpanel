/**
 * Unit tests for ClientCreditHistoryTable: one row per movement with its type, invoice, signed change and resulting balance, and the
 * empty state that says nothing is invented for credit older than the history.
 *
 * @module tests/components/client/credit/HistoryTable.test
 */

import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import HistoryTable from '~/components/client/credit/HistoryTable.vue'
import type { ClientCreditEntry } from '~/types/clientcreditentry'
import { CreditMovement } from '~/types/creditmovement'

/** A credit added by staff. */
const added: ClientCreditEntry = { id: 11, date: '2026-10-01T09:00:00Z', movement: CreditMovement.TopUp, change: 40, balanceAfter: 40, invoiceId: 7 }

/** Credit applied to an invoice. */
const applied: ClientCreditEntry = { id: 12, date: '2026-10-02T09:00:00Z', movement: CreditMovement.InvoiceApplied, change: -15.5, balanceAfter: 24.5, invoiceId: 9 }

/** A movement tied to no invoice. */
const adjusted: ClientCreditEntry = { id: 13, date: '2026-10-03T09:00:00Z', movement: CreditMovement.AdminAdd, change: 5, balanceAfter: 29.5, invoiceId: null }

describe('ClientCreditHistoryTable', () => {
  it('says nothing has moved the balance when there are no lines', () => {
    const wrapper = mountClient(HistoryTable, { props: { entries: [], currency: 'USD' } })

    expect(wrapper.find('[data-testid="credit-history-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-history-table"]').exists()).toBe(false)
  })

  it('lists one row per movement, in the order the backend sent them', () => {
    const wrapper = mountClient(HistoryTable, { props: { entries: [adjusted, applied, added], currency: 'USD' } })

    const rows = wrapper.findAll('[data-testid^="credit-entry-"][data-testid$="1"], [data-testid="credit-entry-12"], [data-testid="credit-entry-13"]')
    expect(rows.length).toBeGreaterThanOrEqual(3)
    const html = wrapper.html()
    expect(html.indexOf('credit-entry-13')).toBeLessThan(html.indexOf('credit-entry-12'))
    expect(html.indexOf('credit-entry-12')).toBeLessThan(html.indexOf('credit-entry-11'))
  })

  it('names each movement by its sentence key', () => {
    const wrapper = mountClient(HistoryTable, { props: { entries: [added, applied, adjusted], currency: 'USD' } })

    expect(wrapper.get('[data-testid="credit-entry-type-11"]').text()).toBe('client.credit.movement.TopUp')
    expect(wrapper.get('[data-testid="credit-entry-type-12"]').text()).toBe('client.credit.movement.InvoiceApplied')
    expect(wrapper.get('[data-testid="credit-entry-type-13"]').text()).toBe('client.credit.movement.AdminAdd')
  })

  it('shows an addition with a plus and a deduction with its minus, and the balance each left', () => {
    const wrapper = mountClient(HistoryTable, { props: { entries: [added, applied], currency: 'USD' } })

    expect(wrapper.get('[data-testid="credit-entry-change-11"]').text()).toMatch(/^\+.*40\.00/)
    expect(wrapper.get('[data-testid="credit-entry-change-12"]').text()).toMatch(/-.*15\.50/)
    expect(wrapper.get('[data-testid="credit-entry-balance-12"]').text()).toContain('24.50')
  })

  it('links a movement to its invoice and shows no link for one that has none', () => {
    const wrapper = mountClient(HistoryTable, { props: { entries: [added, adjusted], currency: 'USD' } })

    expect(wrapper.get('[data-testid="credit-entry-invoice-11"]').attributes('href')).toBe('/client/invoices/7')
    expect(wrapper.find('[data-testid="credit-entry-invoice-13"]').exists()).toBe(false)
  })
})
