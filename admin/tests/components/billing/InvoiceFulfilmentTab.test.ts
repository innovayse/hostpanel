/**
 * Mount tests for InvoiceFulfilmentTab: the records list with plugin, kind, status, attempts and
 * the staff-only last error, the empty and failed-load states, Retry only on a record the API
 * allows to retry, and a confirmed retry that posts, reloads the list and reports the outcome in
 * the tab (the API's own sentence on a refusal).
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceFulfilmentTab from '@/components/billing/InvoiceFulfilmentTab.vue'
import { FulfilmentStatus } from '@/types/invoice'
import { fulfilmentFixture, invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Mounts the tab for invoice 1 and lets the first load settle.
 *
 * @param props - Extra props for the tab.
 * @returns The wrapper.
 */
const mountTab = async (props: Record<string, unknown> = {}) => {
  const wrapper = mountWithApp(InvoiceFulfilmentTab, { props: { invoice: invoiceFixture(), ...props } })
  await flushPromises()
  return wrapper
}

describe('InvoiceFulfilmentTab', () => {
  it('lists the records with plugin, kind, status, attempts and the last error', async () => {
    const mock = stubRoutes({
      'GET /api/billing/1/fulfilment': () =>
        json([
          fulfilmentFixture(),
          fulfilmentFixture({
            id: 32,
            status: FulfilmentStatus.Done,
            attempts: 1,
            lastError: null,
            completedAt: '2026-10-19T09:00:00Z',
            canRetry: false,
          }),
        ]),
    })
    const wrapper = await mountTab()

    const table = wrapper.get('[data-testid="billing-invoice-fulfilment-table"]').text()
    expect(table).toContain('provisioning-demo')
    expect(table).toContain('Failed')
    expect(table).toContain('Done')
    expect(table).toContain('Paid')
    expect(table).toContain('The upstream account is locked.')
    expect(String(mock.mock.calls[0]?.[0])).toBe('/api/billing/1/fulfilment')
  })

  it('gives Done its completion time, Pending its next attempt and Failed no time', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () =>
        json([
          fulfilmentFixture({ id: 31, status: FulfilmentStatus.Failed, nextAttemptAt: '2026-10-20T10:00:00Z' }),
          fulfilmentFixture({
            id: 32,
            status: FulfilmentStatus.Done,
            completedAt: '2026-10-19T09:00:00Z',
            nextAttemptAt: '2026-10-25T10:00:00Z',
          }),
          fulfilmentFixture({ id: 33, status: FulfilmentStatus.Pending, nextAttemptAt: '2026-10-21T11:00:00Z' }),
        ]),
    })
    const wrapper = await mountTab()

    /** The rendered table rows, in the order the records were listed. */
    const rows = wrapper.findAll('tbody tr')
    /** Text of the completed or next attempt cell of one row. */
    const whenOfRow = (index: number): string => rows[index]?.findAll('td')[5]?.text() ?? ''
    expect(whenOfRow(0)).toBe('—')
    expect(whenOfRow(1)).toContain('19/10/2026')
    expect(whenOfRow(2)).toContain('21/10/2026')
  })

  it('clears the outcome banner and closes the retry prompt when the invoice changes', async () => {
    stubRoutes({
      'POST /api/billing/1/fulfilment/31/retry': () => problem('This record cannot be retried.', 409),
      'GET /api/billing/2/fulfilment': () => json([fulfilmentFixture({ id: 41, invoiceId: 2 })]),
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]),
    })
    const wrapper = await mountTab()
    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-feedback"]').exists()).toBe(true)
    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    expect(wrapper.find('[data-testid="dialog-body"]').exists()).toBe(true)

    await wrapper.setProps({ invoice: invoiceFixture({ id: 2 }) })
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-feedback"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dialog-body"]').exists()).toBe(false)
  })

  it('offers Retry only on a record the API allows to retry', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture(), fulfilmentFixture({ id: 32, canRetry: false })]),
    })
    const wrapper = await mountTab()

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-32"]').exists()).toBe(false)
  })

  it('hides Retry while the invoice is read-only', async () => {
    stubRoutes({ 'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]) })
    const wrapper = await mountTab({ readonly: true })

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(false)
  })

  it('says so when the invoice has no records', async () => {
    stubRoutes({ 'GET /api/billing/1/fulfilment': () => json([]) })
    const wrapper = await mountTab()

    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-empty"]').text()).toContain('No fulfilment records')
  })

  it('shows the API sentence with Retry instead of an empty list when the load fails, and loads again', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => {
        calls += 1
        return calls === 1 ? problem('Fulfilment is unavailable.', 500) : json([fulfilmentFixture()])
      },
    })
    const wrapper = await mountTab()

    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-error"]').text()).toContain('Fulfilment is unavailable.')
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-empty"]').exists()).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-fulfilment-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-table"]').text()).toContain('provisioning-demo')
  })

  it('asks first, then posts the retry, reloads the list and confirms in the tab', async () => {
    let retried = false
    const mock = stubRoutes({
      'POST /api/billing/1/fulfilment/31/retry': () => {
        retried = true
        return new Response(null, { status: 204 })
      },
      'GET /api/billing/1/fulfilment': () =>
        json([retried ? fulfilmentFixture({ status: FulfilmentStatus.Pending, canRetry: false }) : fulfilmentFixture()]),
    })
    const wrapper = await mountTab()

    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    expect(wrapper.get('[data-testid="dialog-body"]').text()).toContain('provisioning-demo')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'POST')).toBe(false)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.filter(([url]) => String(url).endsWith('/retry'))).toHaveLength(1)
    expect(mock.mock.calls.filter(([url]) => String(url).endsWith('/fulfilment'))).toHaveLength(2)
    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-feedback"]').text()).toContain('Retry requested.')
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(false)
  })

  it('shows the 409 sentence of a refused retry in the tab', async () => {
    stubRoutes({
      'POST /api/billing/1/fulfilment/31/retry': () => problem('This record cannot be retried.', 409),
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]),
    })
    const wrapper = await mountTab()

    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-fulfilment-feedback"]').text()).toBe('This record cannot be retried.')
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-error"]').exists()).toBe(false)
  })

  it('loads again when the invoice changes', async () => {
    const mock = stubRoutes({
      'GET /api/billing/2/fulfilment': () => json([fulfilmentFixture({ id: 41, invoiceId: 2 })]),
      'GET /api/billing/1/fulfilment': () => json([fulfilmentFixture()]),
    })
    const wrapper = await mountTab()

    await wrapper.setProps({ invoice: invoiceFixture({ id: 2 }) })
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url) === '/api/billing/2/fulfilment')).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-41"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(false)
  })

  it('keeps the records of the new invoice when a retry of the previous one finishes after its load', async () => {
    /** Lets the test finish the retry of invoice 1 on demand. */
    let finishRetry: () => void = () => undefined
    /** The pending answer of the retry POST of invoice 1. */
    const retryAnswer = new Promise<Response>((resolve) => {
      finishRetry = () => resolve(new Response(null, { status: 204 }))
    })
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string, init?: RequestInit) => {
        if (init?.method === 'POST') {
          return retryAnswer
        }
        return Promise.resolve(
          url === '/api/billing/2/fulfilment'
            ? json([fulfilmentFixture({ id: 41, invoiceId: 2 })])
            : json([fulfilmentFixture()]),
        )
      }),
    )
    const wrapper = await mountTab()
    await wrapper.get('[data-testid="billing-invoice-fulfilment-retry-31"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await wrapper.setProps({ invoice: invoiceFixture({ id: 2 }) })
    await flushPromises()
    finishRetry()
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-41"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-31"]').exists()).toBe(false)
  })

  it('drops the load error of the previous invoice when the next invoice loads', async () => {
    stubRoutes({
      'GET /api/billing/1/fulfilment': () => problem('Fulfilment is unavailable.', 500),
      'GET /api/billing/2/fulfilment': () => json([fulfilmentFixture({ id: 41, invoiceId: 2 })]),
    })
    const wrapper = await mountTab()
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-error"]').exists()).toBe(true)

    await wrapper.setProps({ invoice: invoiceFixture({ id: 2 }) })
    await flushPromises()

    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="billing-invoice-fulfilment-retry-41"]').exists()).toBe(true)
  })
})
