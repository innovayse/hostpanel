/**
 * Unit tests for ServiceInvoicesCard: loading, the failure that is never shown as "no invoices",
 * the empty state, the table and the note about invoices no service owns.
 *
 * The real service management store, API composable and transport run against a stub server
 * playing the BFF; the card renders what the store read through it.
 *
 * @module tests/components/client/services/ServiceInvoicesCard.test
 */
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountClient } from '~/tests/helpers/mountClient'
import ServiceInvoicesCard from '~/components/client/services/ServiceInvoicesCard.vue'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { heldReply, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { ServiceInvoice } from '~/types/serviceinvoice'

/** The BFF path that lists the invoices raised against service 4. */
const INVOICES_PATH = '/api/portal/client/services/4/invoices'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds an invoice row.
 *
 * @param id - Invoice id.
 * @returns The invoice.
 */
const invoice = (id: number): ServiceInvoice => ({ id, invoiceDate: '2026-01-01', dueDate: '2026-01-15', total: 10, currency: 'USD', status: 'Paid' })

/**
 * Mounts the card and starts the read the service page starts, through the store.
 *
 * @param reply - What the BFF answers for the invoice list.
 * @returns The wrapper and the read, which settles when the answer has been handled.
 */
const mountCard = (reply: StubReply | (() => Promise<StubReply>)) => {
  table[`GET ${INVOICES_PATH}`] = reply
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mountClient(ServiceInvoicesCard, { pinia })
  const read = useServiceManagementStore(pinia).loadInvoices('4').then(flushPromises)
  return { wrapper, read }
}

/**
 * Mounts the card and waits until the read has been handled and the card has rendered it.
 *
 * @param reply - What the BFF answers for the invoice list.
 * @returns The wrapper.
 */
const mountLoaded = async (reply: StubReply) => {
  const { wrapper, read } = mountCard(reply)
  await read
  return wrapper
}

describe('ServiceInvoicesCard', () => {
  beforeEach(async () => {
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows skeletons while loading', async () => {
    const held = heldReply()
    const { wrapper, read } = mountCard(held.reply)
    await flushPromises()

    expect(wrapper.findAll('[data-testid="ui-skeleton"]')).toHaveLength(3)

    held.release({ body: { invoices: [], unattributedInvoiceCount: 0 } })
    await read
  })

  it('shows the failure, not an empty list, when the read failed', async () => {
    const wrapper = await mountLoaded({ status: 400, body: { detail: 'Could not read invoices.' } })

    expect(wrapper.find('[data-testid="service-invoices-error"]').text()).toBe('Could not read invoices.')
    expect(wrapper.find('[data-testid="service-invoices-empty"]').exists()).toBe(false)
  })

  it('says nothing is recorded against the service when the list is empty', async () => {
    const wrapper = await mountLoaded({ body: { invoices: [], unattributedInvoiceCount: 0 } })

    expect(wrapper.find('[data-testid="service-invoices-empty"]').exists()).toBe(true)
  })

  it('lists the invoices with a link to each', async () => {
    const wrapper = await mountLoaded({ body: { invoices: [invoice(5), invoice(6)], unattributedInvoiceCount: 0 } })

    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
    expect(wrapper.find('[data-testid="service-invoice-5"] a').attributes('href')).toBe('/client/invoices/5')
  })

  it('notes the invoices that no service owns, but not when the read failed', async () => {
    const ok = await mountLoaded({ body: { invoices: [invoice(5)], unattributedInvoiceCount: 3 } })
    const failed = await mountLoaded({ status: 400, body: { detail: 'x' } })

    expect(ok.find('[data-testid="service-invoices-unrecorded"]').exists()).toBe(true)
    expect(failed.find('[data-testid="service-invoices-unrecorded"]').exists()).toBe(false)
  })
})
