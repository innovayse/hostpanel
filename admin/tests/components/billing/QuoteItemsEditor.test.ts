/**
 * Mount tests for QuoteItemsEditor: line and total arithmetic, and the v-model updates for
 * editing, adding and removing lines.
 */
import { describe, expect, it, vi } from 'vitest'
import { nextRowKey } from '@/lib/rowKey'
import QuoteItemsEditor from '@/components/billing/QuoteItemsEditor.vue'
import type { QuoteLineDraft } from '@/types/quote'
import { mountWithApp } from '../../helpers/mount'

/**
 * Builds a quote line.
 *
 * @param overrides - Fields to replace.
 * @returns The line.
 */
const line = (overrides: Partial<QuoteLineDraft> = {}): QuoteLineDraft => ({
  key: nextRowKey(),
  quantity: 2,
  description: 'Hosting',
  unitPrice: 10,
  discountPercent: 50,
  taxed: false,
  ...overrides,
})

describe('QuoteItemsEditor', () => {
  it('totals each line after its discount', () => {
    const wrapper = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line(), line({ discountPercent: 0 })] } })

    expect(wrapper.get('[data-testid="billing-quote-subtotal"]').text()).toBe('30.00 USD')
    expect(wrapper.get('[data-testid="billing-quote-total"]').text()).toBe('30.00 USD')
  })

  it('emits a new array when a line is edited', async () => {
    const onUpdate = vi.fn()
    const wrapper = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line()], 'onUpdate:modelValue': onUpdate } })

    await wrapper.get('[data-testid="billing-quote-item-description-0"]').setValue('Renewal')

    expect(onUpdate).toHaveBeenCalledWith([expect.objectContaining({ description: 'Renewal', quantity: 2 })])
  })

  it('adds a blank line', async () => {
    const onUpdate = vi.fn()
    const wrapper = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line()], 'onUpdate:modelValue': onUpdate } })

    await wrapper.get('[data-testid="billing-quote-item-add"]').trigger('click')

    const [next] = onUpdate.mock.calls[0] as [QuoteLineDraft[]]
    expect(next).toHaveLength(2)
    expect(next[1]).toEqual({ key: expect.any(Number), quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false })
  })

  it('offers removal only while more than one line exists', async () => {
    const onUpdate = vi.fn()
    const single = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line()] } })
    expect(single.find('[data-testid="billing-quote-item-remove-0"]').exists()).toBe(false)
    // The only line has no actions control at all, so nothing can leave the quote empty.
    expect(single.find('[data-testid="row-actions"]').exists()).toBe(false)

    const double = mountWithApp(QuoteItemsEditor, {
      props: { modelValue: [line(), line({ description: 'Second' })], 'onUpdate:modelValue': onUpdate },
    })
    await double.get('[data-testid="billing-quote-item-remove-0"]').trigger('click')

    expect(onUpdate).toHaveBeenCalledWith([expect.objectContaining({ description: 'Second' })])
  })

  it('writes the line, sub total and total in the currency of the quote', () => {
    const wrapper = mountWithApp(QuoteItemsEditor, {
      props: { modelValue: [line({ unitPrice: 24000, quantity: 1, discountPercent: 0 })], currency: 'AMD' },
    })

    expect(wrapper.text()).toContain('24,000.00 AMD')
    expect(wrapper.get('[data-testid="billing-quote-subtotal"]').text()).toBe('24,000.00 AMD')
    expect(wrapper.get('[data-testid="billing-quote-total"]').text()).toBe('24,000.00 AMD')
    expect(wrapper.text()).not.toContain('USD')
  })

  it('draws the totals as the foot of the line table, under the total column', () => {
    const wrapper = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line({ unitPrice: 10, quantity: 3, discountPercent: 0 })] } })

    const cells = wrapper.get('tfoot').findAll('tr')[0]?.findAll('td') ?? []
    expect(cells[0]?.attributes('colspan')).toBe('5')
    expect(cells[1]?.attributes('data-testid')).toBe('billing-quote-subtotal')
  })

  it('keeps the number columns on one line, sized to their content', () => {
    const wrapper = mountWithApp(QuoteItemsEditor, { props: { modelValue: [line({})] } })

    const unitPrice = wrapper.findAll('th').find((th) => th.text() === 'Unit price')
    expect(unitPrice?.classes()).toContain('whitespace-nowrap')
  })
})
