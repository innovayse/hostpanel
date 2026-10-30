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
          items: [{ id: 11, description: 'Managed hosting', unitPrice: 24000, quantity: 1, amount: 24000 }],
        }),
        readonly: true,
      },
    })

    expect(wrapper.get('[data-testid="billing-invoice-items-table"]').text()).toContain('24000.00 AMD')
    expect(wrapper.get('[data-testid="billing-invoice-total-total"]').text()).toBe('24000.00 AMD')
  })

  it('shows only the description and amount of a read-only line, since tax is invoice-wide', () => {
    const wrapper = mountWithApp(InvoiceItemsSection, { props: { invoice: invoiceFixture(), readonly: true } })

    const headers = wrapper.get('[data-testid="billing-invoice-items-table"]').findAll('th').map((th) => th.text())
    expect(headers).toEqual(['Description', 'Amount'])
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
    expect(wrapper.get('[data-testid="billing-invoice-items-feedback"]').text()).toBe('Changes saved successfully.')
  })

  it('sends a removed line as a deletion and keeps the quantity and price of the others', async () => {
    const mock = stubRoutes({
      'PUT /api/billing/1/items': () => new Response(null, { status: 204 }),
      'GET /api/billing/1': () => json(invoiceFixture()),
    })
    const invoice = invoiceFixture({
      items: [
        { id: 11, description: 'Hosting', unitPrice: 10, quantity: 3, amount: 30 },
        { id: 12, description: 'Setup', unitPrice: 5, quantity: 1, amount: 5 },
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
      invoice: invoiceFixture({ items: [{ id: 11, description: 'Renamed', unitPrice: 100, quantity: 1, amount: 100 }] }),
    })

    expect((wrapper.get('[data-testid="billing-invoice-item-description-0"]').element as HTMLTextAreaElement).value)
      .toBe('Renamed')
  })

  it('previews the tax on the sub-total when a tax percentage is given', async () => {
    const wrapper = mountWithApp(InvoiceItemsSection, {
      props: { invoice: invoiceFixture({ amounts: { ...invoiceFixture().amounts, taxRate: 10 } }), taxPercent: 10 },
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

    expect(wrapper.get('[data-testid="billing-invoice-items-feedback"]').text()).toBe('A paid invoice cannot be changed.')
  })
})
