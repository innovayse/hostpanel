/**
 * Mount tests for InvoiceItemsSection: the read-only table with its totals, the editor (add,
 * tick and delete lines) and the save that goes through the billing store, with its outcome
 * shown next to the save button.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'
import { useToastStore } from '@/stores/toast'
import { INVOICE_ITEMS_TABLE } from '@/views/InvoiceDetailView.schema'
import { toastMessages } from '../../helpers/toasts'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InvoiceItemsSection', () => {
  it('shows the lines and the totals read-only', () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture(), readonly: true } })

    expect(wrapper.get('[data-testid="billing-invoice-items-table"]').text()).toContain('Managed hosting')
    expect(wrapper.get('[data-testid="billing-invoice-total-total"]').text()).toBe('100.00 USD')
    expect(wrapper.find('[data-testid="billing-invoice-items-save"]').exists()).toBe(false)
  })

  it('uses the invoice currency for the line and total values when the invoice is not USD', () => {
    const wrapper = mountWithApp(InvoiceItemsSection, {
      props: {
        invoice: invoiceFixture({
          currency: 'AMD',
          amounts: { ...invoiceFixture().amounts, total: 24000, subTotal: 24000 },
          items: [{ id: 11, description: 'Managed hosting', unitPrice: 24000, quantity: 1, amount: 24000, sku: null }],
        }),
        readonly: true,
      },
    })

    expect(wrapper.get('[data-testid="billing-invoice-items-table"]').text()).toContain('24,000.00 AMD')
    expect(wrapper.get('[data-testid="billing-invoice-items-table"]').text()).toContain('24,000.00 AMD')
  })

  it('shows only the description and amount of a read-only line, since tax is invoice-wide', () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture(), readonly: true } })

    const headers = wrapper.get('[data-testid="billing-invoice-items-table"]').findAll('th').map((th) => th.text())
    expect(headers).toEqual(['Description', 'Amount'])
    // The captions are drawn from the invoice screen's schema, so the skeleton and the real card cannot differ.
    expect(headers).toHaveLength(INVOICE_ITEMS_TABLE.columns.length)
  })

  it('adds a line, removes it again, and deletes a ticked line', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-items-add"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-invoice-item-description-1"]').exists()).toBe(true)

    await wrapper.get('[data-testid="billing-invoice-item-remove-1"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-invoice-item-description-1"]').exists()).toBe(false)

    await wrapper.get('[data-testid="billing-invoice-item-select-0"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-items-delete-selected"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-invoice-item-description-0"]').exists()).toBe(false)
  })

  it('saves the lines through the store and confirms it', async () => {
    const mock = stubRoutes({
      'PUT /api/billing/1/items': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-items-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toEqual({
      items: [{ id: 11, description: 'Managed hosting', unitPrice: 100, quantity: 1, isDeleted: false }],
    })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Changes saved successfully.'])
  })

  it('sends a removed line as a deletion and keeps the quantity and price of the others', async () => {
    const mock = stubRoutes({
      'PUT /api/billing/1/items': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const invoice = invoiceFixture({
      items: [
        { id: 11, description: 'Hosting', unitPrice: 10, quantity: 3, amount: 30, sku: null },
        { id: 12, description: 'Setup', unitPrice: 5, quantity: 1, amount: 5, sku: null },
      ],
    })
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice } })
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-item-remove-1"]').trigger('click')
    await wrapper.get('[data-testid="billing-invoice-items-save"]').trigger('click')
    await flushPromises()

    const put = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse(String((put?.[1] as RequestInit | undefined)?.body))).toEqual({
      items: [
        { id: 11, description: 'Hosting', unitPrice: 10, quantity: 3, isDeleted: false },
        { id: 12, description: '', unitPrice: 0, quantity: 1, isDeleted: true },
      ],
    })
  })

  it('keeps unsaved line edits when the invoice is reloaded by another save', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })
    await flushPromises()

    await wrapper.get('[data-testid="billing-invoice-item-description-0"]').setValue('Edited line')
    await wrapper.setProps({ invoice: invoiceFixture({ notes: 'saved elsewhere' }) })

    expect((wrapper.get('[data-testid="billing-invoice-item-description-0"]').element as HTMLTextAreaElement).value)
      .toBe('Edited line')
  })

  it('shows the reloaded lines once the editor is clean', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })

    await wrapper.setProps({
      invoice: invoiceFixture({ items: [{ id: 11, description: 'Renamed', unitPrice: 100, quantity: 1, amount: 100, sku: null }] }),
    })

    expect((wrapper.get('[data-testid="billing-invoice-item-description-0"]').element as HTMLTextAreaElement).value)
      .toBe('Renamed')
  })

  it('shows the saved tax in the read-only totals when the invoice has a tax rate', () => {
    const base = invoiceFixture()
    const wrapper = mountWithApp(InvoiceItemsSection, {
      props: { invoice: invoiceFixture({ amounts: { ...base.amounts, taxRate: 10, tax: 10, total: 105 } }), readonly: true },
    })

    expect(wrapper.get('[data-testid="billing-invoice-total-tax"]').text()).toBe('10.00 USD')
  })

  it('shows no tax row without a tax rate or under the reverse charge', () => {
    const base = invoiceFixture()
    const plain = mountWithApp(InvoiceItemsSection, { props: { invoice: base, readonly: true } })
    const reverse = mountWithApp(InvoiceItemsSection, {
      props: { invoice: invoiceFixture({ reverseCharge: true, amounts: { ...base.amounts, taxRate: 10 } }), readonly: true },
    })

    expect(plain.find('[data-testid="billing-invoice-total-tax"]').exists()).toBe(false)
    expect(reverse.find('[data-testid="billing-invoice-total-tax"]').exists()).toBe(false)
  })

  it('previews the tax on the live sub-total at the invoice rate while editing', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, {
      props: { invoice: invoiceFixture({ amounts: { ...invoiceFixture().amounts, taxRate: 10 } }) },
    })
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-invoice-total-tax"]').text()).toBe('10.00 USD')
    expect(wrapper.get('[data-testid="billing-invoice-total-total"]').text()).toBe('105.00 USD')
  })

  it('shows the API sentence next to the save button when the save is refused', async () => {
    stubRoutes({ 'PUT /api/billing/1/items': () => problem('A paid invoice cannot be changed.', 409) })
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })

    await wrapper.get('[data-testid="billing-invoice-items-save"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('A paid invoice cannot be changed.')
  })

  it('shows the editable line total in the invoice currency and follows the typed price', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, {
      props: {
        invoice: invoiceFixture({
          currency: 'AMD',
          items: [{ id: 11, description: 'Managed hosting', unitPrice: 24000, quantity: 2, amount: 48000, sku: null }],
        }),
      },
    })

    await flushPromises()
    expect(wrapper.get('[data-testid="billing-invoice-items-editor"]').text()).toContain('48,000.00 AMD')

    await wrapper.get('[data-testid="billing-invoice-item-price-0"] input').setValue('1000')

    const table = wrapper.get('[data-testid="billing-invoice-items-editor"]').text()
    expect(table).toContain('2,000.00 AMD')
    expect(table).not.toContain('$')
  })

  it('draws the totals as the foot of the table, their figures in the amount column', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })
    await flushPromises()

    const foot = wrapper.get('[data-testid="billing-invoice-items-editor"] tfoot')
    const first = foot.findAll('tr')[0]?.findAll('td') ?? []
    expect(first[0]?.attributes('colspan')).toBe('4')
    expect(first[1]?.attributes('data-testid')).toBe('billing-invoice-total-subtotal')
    expect(first[1]?.classes()).toContain('text-end')
  })

  it('keeps the quantity and unit price headers on one line, sized to their content', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })
    await flushPromises()

    const headers = wrapper.get('[data-testid="billing-invoice-items-editor"]').findAll('th')
    const unitPrice = headers.find((th) => th.text() === 'Unit price')
    expect(unitPrice?.classes()).toContain('whitespace-nowrap')
    expect(unitPrice?.classes()).toContain('w-px')
  })

  it('ends the editor with the shared action row inside the card, Cancel before Save', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture() } })
    await flushPromises()

    const row = wrapper.get('[data-testid="billing-invoice-items"] [data-testid="ui-form-actions"]')
    const ids = row.findAll('button').map((button) => button.attributes('data-testid'))
    expect(ids).toEqual(['billing-invoice-items-cancel', 'billing-invoice-items-save'])
  })

  it('draws no action row read-only', () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture(), readonly: true } })

    expect(wrapper.find('[data-testid="ui-form-actions"]').exists()).toBe(false)
  })
})
