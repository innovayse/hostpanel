/**
 * Mount tests for OfflineCCView: the submit guard (invoice, date, amount, reference, fees), the exact
 * payload and URL posted, the form cleared after a success, the field errors shown beside their
 * inputs, the API sentence on refusal, and the single router push to the invoice after a success.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { PaymentGateway } from '@/types/gateway'
import OfflineCCView from '@/views/OfflineCCView.vue'
import { invalid, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Fills the invoice, date, amount and reference fields the API needs.
 *
 * @param wrapper - The mounted view.
 * @param reference - Gateway reference to type; empty leaves the field blank.
 * @returns Resolves when the inputs have updated.
 */
const fill = async (wrapper: VueWrapper, reference = 'ref-1'): Promise<void> => {
  await wrapper.get('[data-testid="offline-cc-invoice"] input').setValue('7')
  wrapper.findComponent(UiDatePicker).vm.$emit('update:modelValue', '2026-10-20')
  await wrapper.get('[data-testid="offline-cc-amount"] input').setValue('25')
  if (reference !== '') {
    await wrapper.get('[data-testid="offline-cc-reference"]').setValue(reference)
  }
}

/**
 * Whether the submit button is disabled.
 *
 * @param wrapper - The mounted view.
 * @returns True when disabled.
 */
const submitDisabled = (wrapper: VueWrapper): boolean =>
  wrapper.get('[data-testid="offline-cc-submit"]').attributes('disabled') !== undefined

describe('OfflineCCView', () => {
  it('shows its title and keeps submit disabled until the form is complete', async () => {
    const { wrapper } = await mountScreen(OfflineCCView)

    expect(wrapper.get('[data-testid="offline-cc-header"]').text()).toContain('Offline Credit Card Processing')
    expect(submitDisabled(wrapper)).toBe(true)

    await fill(wrapper)

    expect(submitDisabled(wrapper)).toBe(false)
  })

  it('keeps submit disabled while the reference is empty', async () => {
    const { wrapper } = await mountScreen(OfflineCCView)

    await fill(wrapper, '')

    expect(submitDisabled(wrapper)).toBe(true)
  })

  it('never lets a negative fee through: the number input clamps it to zero', async () => {
    const { wrapper } = await mountScreen(OfflineCCView)
    await fill(wrapper)

    await wrapper.get('[data-testid="offline-cc-fees"] input').setValue('-1')
    await wrapper.get('[data-testid="offline-cc-fees"] input').trigger('blur')

    expect((wrapper.get('[data-testid="offline-cc-fees"] input').element as HTMLInputElement).value).toBe('0')
    expect(submitDisabled(wrapper)).toBe(false)
  })

  it('posts the full payload to the invoice, confirms it, clears the form and opens the invoice', async () => {
    const mock = stubRoutes({ 'POST /api/billing/7/payment': () => new Response(null, { status: 204 }) })
    const { wrapper, router } = await mountScreen(OfflineCCView)

    await fill(wrapper)
    await wrapper.get('[data-testid="offline-cc-form"]').trigger('submit')
    await flushPromises()

    const posts = mock.mock.calls.filter(([url]) => String(url).endsWith('/billing/7/payment'))
    expect(posts).toHaveLength(1)
    expect(JSON.parse(String((posts[0]?.[1] as RequestInit | undefined)?.body))).toEqual({
      date: new Date('2026-10-20').toISOString(),
      gateway: PaymentGateway.Stripe,
      transactionId: 'ref-1',
      amount: 25,
      fees: 0,
    })
    expect(wrapper.get('[data-testid="offline-cc-feedback"]').text()).toBe('Payment recorded.')
    expect((wrapper.get('[data-testid="offline-cc-invoice"] input').element as HTMLInputElement).value).toBe('0')
    expect((wrapper.get('[data-testid="offline-cc-amount"] input').element as HTMLInputElement).value).toBe('0')
    expect((wrapper.get('[data-testid="offline-cc-reference"]').element as HTMLInputElement).value).toBe('')
    expect(submitDisabled(wrapper)).toBe(true)

    await wrapper.get('[data-testid="offline-cc-open-invoice"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/billing/invoices/7/view')
  })

  it('shows the API sentence under the form when the payment is refused', async () => {
    stubRoutes({ 'POST /api/billing/7/payment': () => problem('The amount exceeds the balance.', 409) })
    const { wrapper } = await mountScreen(OfflineCCView)

    await fill(wrapper)
    await wrapper.get('[data-testid="offline-cc-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="offline-cc-feedback"]').text()).toBe('The amount exceeds the balance.')
    expect(wrapper.find('[data-testid="offline-cc-open-invoice"]').exists()).toBe(false)
    expect((wrapper.get('[data-testid="offline-cc-reference"]').element as HTMLInputElement).value).toBe('ref-1')
  })

  it('shows the field errors beside their inputs when the API refuses a field', async () => {
    stubRoutes({
      'POST /api/billing/7/payment': () =>
        invalid('Check the form.', { amount: ['Amount must be positive.'], transactionId: ['Reference is required.'] }),
    })
    const { wrapper } = await mountScreen(OfflineCCView)

    await fill(wrapper)
    await wrapper.get('[data-testid="offline-cc-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Amount must be positive.')
    expect(wrapper.text()).toContain('Reference is required.')
    expect(wrapper.get('[data-testid="offline-cc-reference"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.get('[data-testid="offline-cc-amount"] input').attributes('aria-invalid')).toBe('true')
  })
})
