/**
 * Tests for `pages/client/invoices/index.vue` against the real client store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the table, the
 * filter tabs and the failure state.
 *
 * @module tests/pages/client/invoices/index.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import InvoicesPage from '~/pages/client/invoices/index.vue'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { ClientInvoice } from '~/types/clientinvoice'
import { InvoiceStatus } from '~/types/invoicestatus'
import { Permission } from '~/types/permission'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads. */
const PATHS = {
  me: '/api/portal/client/me',
  invoices: '/api/portal/client/invoices',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds an invoice with the fields the table reads.
 *
 * @param id - Invoice id.
 * @param status - Invoice status.
 * @returns An invoice.
 */
const invoice = (id: number, status: InvoiceStatus): ClientInvoice =>
  ({ id, status, currency: 'USD', total: 25, invoiceDate: '2026-01-01', dueDate: '2099-01-15' } as ClientInvoice)

/**
 * Lists the ids of the rows the table shows.
 *
 * @param wrapper - The mounted page.
 * @returns The ids, in order.
 */
const rowIds = (wrapper: Awaited<ReturnType<typeof mountPage>>): string[] =>
  wrapper.findAll('[data-testid^="invoice-row-"]').map((row) => row.attributes('data-testid')!.replace('invoice-row-', ''))

beforeEach(async () => {
  stubClientRuntime()
  table = {
    [`GET ${PATHS.me}`]: { body: makeClientUser({ permissions: Permission.All }) },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('invoice list page', () => {
  it('lists every invoice with its amount and a link', async () => {
    table[`GET ${PATHS.invoices}`] = { body: [invoice(1, InvoiceStatus.Unpaid), invoice(2, InvoiceStatus.Paid)] }

    const wrapper = await mountPage(InvoicesPage)
    await waitUntil(() => expect(rowIds(wrapper)).toEqual(['1', '2']))

    expect(wrapper.text()).toContain('$25.00')
    expect(wrapper.find('[data-testid="invoice-view-1"]').attributes('href')).toBe('/client/invoices/1')
  })

  it('counts both owing statuses under the unpaid tab', async () => {
    table[`GET ${PATHS.invoices}`] = { body: [invoice(1, InvoiceStatus.Unpaid), invoice(2, InvoiceStatus.Overdue), invoice(3, InvoiceStatus.Paid)] }

    const wrapper = await mountPage(InvoicesPage)
    await waitUntil(() => expect(rowIds(wrapper)).toHaveLength(3))
    await wrapper.find('[data-testid="ui-tab-current"]').trigger('click')

    expect(rowIds(wrapper)).toEqual(['1', '2'])
  })

  it('shows only the paid invoices under the paid tab, a partially refunded one included', async () => {
    table[`GET ${PATHS.invoices}`] = {
      body: [
        invoice(1, InvoiceStatus.Unpaid),
        invoice(2, InvoiceStatus.Paid),
        invoice(3, InvoiceStatus.Cancelled),
        invoice(4, InvoiceStatus.PartiallyRefunded),
        invoice(5, InvoiceStatus.Refunded)
      ]
    }

    const wrapper = await mountPage(InvoicesPage)
    await waitUntil(() => expect(rowIds(wrapper)).toHaveLength(5))
    await wrapper.find('[data-testid="ui-tab-finished"]').trigger('click')

    expect(rowIds(wrapper)).toEqual(['2', '4'])
  })

  it('shows the empty state without invoices', async () => {
    table[`GET ${PATHS.invoices}`] = { body: [] }

    const wrapper = await mountPage(InvoicesPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.invoices.empty')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    table[`GET ${PATHS.invoices}`] = { status: 503, body: { detail: 'Billing is down.' } }

    const wrapper = await mountPage(InvoicesPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="invoices-error"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="invoices-error"]').text()).toBe('Billing is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })
})
