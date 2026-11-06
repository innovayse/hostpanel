/**
 * Mount tests for CreditNoteIssueDialog: the totals follow the typed lines at the invoice's tax rate, a note above what the invoice
 * can still be credited is refused before the call, a missing reason or line stays in the form with its sentence, the invoice lines
 * can be taken as they are, and a valid form emits the request body.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import CreditNoteIssueDialog from '@/components/billing/CreditNoteIssueDialog.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { CreditNoteMethod } from '@/types/creditnote'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { mountWithApp } from '../../helpers/mount'

/**
 * Builds a paid invoice of 100 with 20% tax that can still be credited 120.
 *
 * @param creditable - What the invoice can still be credited.
 * @returns The invoice.
 */
const paid = (creditable = 120) =>
  invoiceFixture({
    creditableAmount: creditable,
    amounts: { subTotal: 100, credit: 0, tax: 20, taxRate: 20, total: 120 },
    items: [{ id: 1, description: 'Hosting', unitPrice: 100, quantity: 1, amount: 100, sku: null }],
  })

/**
 * Types one line into the form.
 *
 * @param wrapper - The mounted dialog.
 * @param description - The description.
 * @param price - The unit price.
 */
const typeLine = async (wrapper: ReturnType<typeof mountWithApp>, description: string, price: string): Promise<void> => {
  await wrapper.get('[data-testid="billing-credit-note-line-0-description"]').setValue(description)
  await wrapper.get('[data-testid="billing-credit-note-line-0-price"] input').setValue(price)
}

describe('CreditNoteIssueDialog', () => {
  it('says plainly that a credit note is not invoice credit', () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    expect(wrapper.get('[data-testid="billing-credit-note-dialog-explain"]').text()).toContain('not invoice credit')
  })

  it('totals the typed line with the tax at the invoice rate', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await typeLine(wrapper, 'Outage', '50')

    expect(wrapper.get('[data-testid="billing-credit-note-totals"]').text()).toContain('Tax at 20% (the invoice rate): 10.00')
    expect(wrapper.get('[data-testid="billing-credit-note-total"]').text()).toContain('60.00')
    expect(wrapper.find('[data-testid="billing-credit-note-over"]').exists()).toBe(false)
  })

  it('refuses a note above what the invoice can still be credited and sends nothing', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid(54) } })

    await typeLine(wrapper, 'Outage', '50')
    await wrapper.get('[data-testid="billing-credit-note-reason"]').setValue('Because')
    await wrapper.get('[data-testid="billing-credit-note-submit"]').trigger('click')

    expect(wrapper.find('[data-testid="billing-credit-note-over"]').exists()).toBe(true)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('keeps the form open with a sentence when the reason or the line is missing', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await wrapper.get('[data-testid="billing-credit-note-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="billing-credit-note-reason-error"]').text()).toBe('Give a reason for the credit note.')
    expect(wrapper.find('[data-testid="billing-credit-note-lines-error"]').exists()).toBe(true)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('takes the invoice lines as they are, so the whole invoice is credited in one click', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await wrapper.get('[data-testid="billing-credit-note-use-invoice-lines"]').trigger('click')

    expect(wrapper.get<HTMLInputElement>('[data-testid="billing-credit-note-line-0-description"]').element.value).toBe('Hosting')
    expect(wrapper.get('[data-testid="billing-credit-note-total"]').text()).toContain('120.00')
  })

  it('adds and removes lines but never removes the last one', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await wrapper.get('[data-testid="billing-credit-note-add-line"]').trigger('click')
    expect(wrapper.find('[data-testid="billing-credit-note-line-1"]').exists()).toBe(true)
    await wrapper.get('[data-testid="billing-credit-note-line-1-remove"]').trigger('click')

    expect(wrapper.find('[data-testid="billing-credit-note-line-1"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="billing-credit-note-line-0-remove"]').attributes('disabled')).toBeDefined()
  })

  it('emits the request body of a valid form, with the chosen method', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await typeLine(wrapper, 'Outage', '50')
    await wrapper.get('[data-testid="billing-credit-note-reason"]').setValue('  Two days down ')
    wrapper.findComponent(UiSelect).vm.$emit('update:modelValue', CreditNoteMethod.GatewayRefund)
    await flushPromises()
    await wrapper.get('[data-testid="billing-credit-note-submit"]').trigger('click')

    expect(wrapper.emitted('submit')?.[0]).toEqual([
      { items: [{ description: 'Outage', unitPrice: 50, quantity: 1 }], method: CreditNoteMethod.GatewayRefund, reason: 'Two days down' },
    ])
  })

  it('shows the sentence of a refused issue inside the dialog', () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid(), error: 'Another refund is still waiting for its gateway.' } })

    expect(wrapper.get('[data-testid="billing-credit-note-dialog-error"]').text()).toBe('Another refund is still waiting for its gateway.')
  })

  it('emits close when cancelled', async () => {
    const wrapper = mountWithApp(CreditNoteIssueDialog, { props: { invoice: paid() } })

    await wrapper.get('[data-testid="billing-credit-note-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
