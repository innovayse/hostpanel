/**
 * Unit tests for ClientInvoicesApplyCreditCard: it appears only while there is credit to spend and money owing on an invoice that is
 * not a top-up, caps the amount at the smaller of the two, applies it through the credit store and tells the page to read the
 * invoice again.
 *
 * The real credit store, billing API composable and transport run against a stub server playing the BFF; the assertions are on the
 * requests that reached it.
 *
 * @module tests/components/client/invoices/ApplyCreditCard.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ApplyCreditCard from '~/components/client/invoices/ApplyCreditCard.vue'
import { useCreditStore } from '~/stores/credit'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { ClientCredit } from '~/types/clientcredit'
import type { ClientInvoice } from '~/types/clientinvoice'
import { InvoiceStatus } from '~/types/invoicestatus'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF path that answers the credit balance and its history. */
const CREDIT_PATH = '/api/portal/client/credit'

/** The BFF path that applies credit to invoice 41. */
const APPLY_PATH = '/api/portal/client/invoices/41/credit'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a credit.
 *
 * @param balance - The balance.
 * @returns The credit.
 */
const credit = (balance: number): ClientCredit => ({
  balance,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
})

/**
 * Makes the BFF answer the credit with a balance.
 *
 * @param balance - The balance.
 */
const answerBalance = (balance: number): void => {
  table[`GET ${CREDIT_PATH}`] = { body: credit(balance) }
}

/**
 * Builds an invoice.
 *
 * @param overrides - Fields to set.
 * @returns The invoice.
 */
const invoice = (overrides: Record<string, unknown> = {}): ClientInvoice => ({
  id: 41,
  status: InvoiceStatus.Unpaid,
  currency: 'USD',
  total: 100,
  amountDue: 100,
  isCreditTopUp: false,
  isBankTransferPending: false,
  ...overrides
}) as unknown as ClientInvoice

/**
 * Mounts the card and waits for the credit it reads when it appears.
 *
 * @param target - The invoice to apply credit to.
 * @returns The wrapper.
 */
const mountCard = async (target: ClientInvoice) => {
  const wrapper = mountClient(ApplyCreditCard, { props: { invoice: target } })
  await waitUntil(() => expect(requestsTo(stub.requests, CREDIT_PATH)).toHaveLength(1))
  await waitUntil(() => expect(useCreditStore().loading).toBe(false))
  await wrapper.vm.$nextTick()
  return wrapper
}

describe('ClientInvoicesApplyCreditCard', () => {
  beforeEach(async () => {
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('is shown for an owing invoice when there is credit to spend', async () => {
    answerBalance(60)

    const wrapper = await mountCard(invoice())

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="apply-credit-available"]').text()).toBe('client.credit.apply.available')
  })

  it('is not shown when the balance is zero', async () => {
    answerBalance(0)

    const wrapper = await mountCard(invoice())

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('is not shown for a credit top-up invoice: credit cannot pay for credit', async () => {
    answerBalance(60)

    const wrapper = await mountCard(invoice({ isCreditTopUp: true }))

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it.each([InvoiceStatus.Paid, InvoiceStatus.Cancelled, InvoiceStatus.PaymentPending, InvoiceStatus.Refunded])(
    'is not shown for an invoice that is %s',
    async (status) => {
      answerBalance(60)

      const wrapper = await mountCard(invoice({ status, amountDue: 0 }))

      expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
    }
  )

  it('fills the field with the smaller of the balance and the amount due', async () => {
    answerBalance(30)
    const wrapper = await mountCard(invoice({ amountDue: 100 }))

    await wrapper.get('[data-testid="apply-credit-max"]').trigger('click')

    expect((wrapper.get('[data-testid="apply-credit-amount"]').element as HTMLInputElement).value).toBe('30')
  })

  it('caps the most it offers at what the invoice still owes', async () => {
    answerBalance(500)
    const wrapper = await mountCard(invoice({ amountDue: 42.5 }))

    await wrapper.get('[data-testid="apply-credit-max"]').trigger('click')

    expect((wrapper.get('[data-testid="apply-credit-amount"]').element as HTMLInputElement).value).toBe('42.5')
  })

  it.each(['0', '-1', 'abc', '', '61', '10.123'])('refuses %j without asking the backend', async (value) => {
    answerBalance(60)
    const wrapper = await mountCard(invoice({ amountDue: 100 }))

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue(value)
    await wrapper.get('form').trigger('submit')

    expect(requestsTo(stub.requests, APPLY_PATH)).toHaveLength(0)
    expect(wrapper.text()).toContain('client.credit.apply.amountInvalid')
  })

  it('applies the amount, reads the balance again and tells the page to read the invoice again', async () => {
    const balances = [credit(60), credit(40)]
    table[`GET ${CREDIT_PATH}`] = (): StubReply => ({ body: balances[Math.min(requestsTo(stub.requests, CREDIT_PATH).length, balances.length) - 1] })
    table[`POST ${APPLY_PATH}`] = { body: {} }
    const wrapper = await mountCard(invoice())

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('20')
    await wrapper.get('form').trigger('submit')

    await waitUntil(() => expect(wrapper.emitted('applied')).toHaveLength(1))
    expect(requestsTo(stub.requests, APPLY_PATH)).toHaveLength(1)
    expect(bodyOf(requestsTo(stub.requests, APPLY_PATH)[0])).toEqual({ amount: 20 })
    expect(requestsTo(stub.requests, CREDIT_PATH)).toHaveLength(2)
  })

  it('shows the backend sentence and stays quiet to the page when the application is refused', async () => {
    answerBalance(60)
    table[`POST ${APPLY_PATH}`] = { status: 400, body: { detail: 'The balance is short.' } }
    const wrapper = await mountCard(invoice())

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('20')
    await wrapper.get('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('The balance is short.'))
    expect(wrapper.emitted('applied')).toBeUndefined()
  })
})
