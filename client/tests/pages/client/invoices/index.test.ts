/**
 * Tests for `pages/client/invoices/index.vue`: the table, the filter tabs and the failure state.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import InvoicesPage from '~/pages/client/invoices/index.vue'
import type { ClientInvoice } from '~/types/clientinvoice'
import { InvoiceStatus } from '~/types/invoicestatus'

/** The mocked client endpoints the store reaches. */
const clientApi = vi.hoisted(() => ({ fetchInvoices: vi.fn() }))

vi.mock('~/composables/apis/useClientApi', () => ({ useClientApi: () => clientApi }))

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

describe('invoice list page', () => {
  beforeEach(() => {
    clientApi.fetchInvoices.mockReset()
    stubClientRuntime()
  })

  it('lists every invoice with its amount and a link', async () => {
    clientApi.fetchInvoices.mockResolvedValue([invoice(1, InvoiceStatus.Unpaid), invoice(2, InvoiceStatus.Paid)])

    const wrapper = await mountPage(InvoicesPage)

    expect(rowIds(wrapper)).toEqual(['1', '2'])
    expect(wrapper.text()).toContain('$25.00')
    expect(wrapper.find('[data-testid="invoice-view-1"]').attributes('href')).toBe('/client/invoices/1')
  })

  it('counts both owing statuses under the unpaid tab', async () => {
    clientApi.fetchInvoices.mockResolvedValue([invoice(1, InvoiceStatus.Unpaid), invoice(2, InvoiceStatus.Overdue), invoice(3, InvoiceStatus.Paid)])

    const wrapper = await mountPage(InvoicesPage)
    await wrapper.find('[data-testid="ui-tab-current"]').trigger('click')

    expect(rowIds(wrapper)).toEqual(['1', '2'])
  })

  it('shows only the paid invoices under the paid tab, a partially refunded one included', async () => {
    clientApi.fetchInvoices.mockResolvedValue([
      invoice(1, InvoiceStatus.Unpaid),
      invoice(2, InvoiceStatus.Paid),
      invoice(3, InvoiceStatus.Cancelled),
      invoice(4, InvoiceStatus.PartiallyRefunded),
      invoice(5, InvoiceStatus.Refunded)
    ])

    const wrapper = await mountPage(InvoicesPage)
    await wrapper.find('[data-testid="ui-tab-finished"]').trigger('click')

    expect(rowIds(wrapper)).toEqual(['2', '4'])
  })

  it('shows the empty state without invoices', async () => {
    clientApi.fetchInvoices.mockResolvedValue([])

    const wrapper = await mountPage(InvoicesPage)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.invoices.empty')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    clientApi.fetchInvoices.mockRejectedValue({ data: { detail: 'Billing is down.' } })

    const wrapper = await mountPage(InvoicesPage)

    expect(wrapper.find('[data-testid="invoices-error"]').text()).toBe('Billing is down.')
    expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(false)
  })
})
