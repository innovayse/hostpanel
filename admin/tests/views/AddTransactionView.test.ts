/**
 * Mount tests for AddTransactionView: the form posts what was typed and returns to the ledger,
 * and a refusal shows the API sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AddTransactionView from '@/views/AddTransactionView.vue'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** The clients endpoint answer: nobody to pick. */
const noClients = () => json({ items: [], totalCount: 0 })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AddTransactionView', () => {
  it('posts the typed transaction and returns to the ledger', async () => {
    const mock = stubRoutes({
      'GET /api/clients': noClients,
      'POST /api/transactions': () => new Response(null, { status: 204 }),
    })
    const { wrapper, router } = await mountRouted(AddTransactionView, '/billing/add-transaction', '/billing/add-transaction')
    await flushPromises()

    await wrapper.get('[data-testid="billing-transaction-description"]').setValue('Domain renewal')
    await wrapper.get('[data-testid="billing-transaction-amount-in"] input').setValue('45')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse(String((post?.[1] as RequestInit | undefined)?.body))).toMatchObject({
      description: 'Domain renewal',
      amountIn: 45,
      invoiceId: null,
      addToCredit: false,
    })
    expect(router.currentRoute.value.path).toBe('/billing/transactions')
  })

  it('shows the API sentence and stays when the transaction is refused', async () => {
    stubRoutes({ 'GET /api/clients': noClients, 'POST /api/transactions': () => problem('Client is required.', 400) })
    const { wrapper, router } = await mountRouted(AddTransactionView, '/billing/add-transaction', '/billing/add-transaction')
    await flushPromises()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-transaction-error"]').text()).toContain('Client is required.')
    expect(router.currentRoute.value.path).toBe('/billing/add-transaction')
  })
})
