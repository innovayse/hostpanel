/**
 * Tests for the shared payment form: the submit guard (date, amount, fees, optional reference), the
 * gateway options and default, the exact payload handed to the recording action, the reset after a
 * success and the outcome lines for success and refusal.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { DEFAULT_PAYMENT_GATEWAY, useInvoicePaymentForm } from '@/composables/useInvoicePaymentForm'
import type { InvoicePaymentForm, InvoicePaymentFormOptions } from '@/composables/useInvoicePaymentForm'
import { PaymentGateway } from '@/types/gateway'
import { UiTone } from '@/utils/ui/tone'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a throwaway component that owns the form, so the i18n and unmount hooks are real.
 *
 * @param options - Options handed to the composable.
 * @returns The form.
 */
const mountForm = (options: InvoicePaymentFormOptions): InvoicePaymentForm => {
  let form: InvoicePaymentForm | undefined
  mount(defineComponent({
    setup: () => {
      form = useInvoicePaymentForm(options)
      return () => h('div')
    },
  }), { global: { plugins: [createTestI18n()] } })
  return form as InvoicePaymentForm
}

/**
 * Builds options whose recording action resolves to the given outcome.
 *
 * @param ok - Whether the recording succeeds.
 * @param extra - Overrides for the other options.
 * @returns The options and the recording spy.
 */
const optionsFor = (ok: boolean, extra: Partial<InvoicePaymentFormOptions> = {}) => {
  const record = vi.fn(() => Promise.resolve(ok))
  const options: InvoicePaymentFormOptions = {
    record,
    failure: () => 'The amount exceeds the balance.',
    successMessage: () => 'Recorded.',
    ...extra,
  }
  return { options, record }
}

/**
 * Fills the fields every payment needs.
 *
 * @param form - The form to fill.
 */
const fill = (form: InvoicePaymentForm): void => {
  form.paymentDate.value = '2026-10-20'
  form.amount.value = 25
}

describe('useInvoicePaymentForm', () => {
  it('starts on the default gateway and offers every gateway but none', () => {
    const form = mountForm(optionsFor(true).options)

    expect(form.gateway.value).toBe(DEFAULT_PAYMENT_GATEWAY)
    expect(form.gateway.value).toBe(PaymentGateway.Stripe)
    expect(form.gatewayOptions.value.map((option) => option.value)).toEqual([
      PaymentGateway.BankTransfer,
      PaymentGateway.Stripe,
      PaymentGateway.PayPal,
    ])
  })

  it('needs a date and a positive amount', () => {
    const form = mountForm(optionsFor(true).options)
    expect(form.canSubmit.value).toBe(false)

    form.paymentDate.value = '2026-10-20'
    expect(form.canSubmit.value).toBe(false)

    form.amount.value = 25
    expect(form.canSubmit.value).toBe(true)
  })

  it('refuses a negative fee', () => {
    const form = mountForm(optionsFor(true).options)
    fill(form)

    form.fees.value = -1

    expect(form.canSubmit.value).toBe(false)
  })

  it('needs a reference only when the caller requires one', () => {
    const optional = mountForm(optionsFor(true).options)
    fill(optional)
    expect(optional.canSubmit.value).toBe(true)

    const required = mountForm(optionsFor(true, { requireReference: true }).options)
    fill(required)
    expect(required.canSubmit.value).toBe(false)

    required.transactionId.value = '   '
    expect(required.canSubmit.value).toBe(false)

    required.transactionId.value = 'ref-1'
    expect(required.canSubmit.value).toBe(true)
  })

  it('does not record an incomplete form', async () => {
    const { options, record } = optionsFor(true)
    const form = mountForm(options)

    expect(await form.submit(7)).toBe(false)
    expect(record).not.toHaveBeenCalled()
  })

  it('hands the full payload to the recording action and clears the form on success', async () => {
    const { options, record } = optionsFor(true)
    const form = mountForm(options)
    fill(form)
    form.gateway.value = PaymentGateway.PayPal
    form.transactionId.value = ' ref-1 '
    form.fees.value = 1.5

    expect(await form.submit(7)).toBe(true)

    expect(record).toHaveBeenCalledWith(7, {
      date: new Date('2026-10-20').toISOString(),
      gateway: PaymentGateway.PayPal,
      transactionId: 'ref-1',
      amount: 25,
      fees: 1.5,
    })
    expect(form.paymentDate.value).toBeNull()
    expect(form.gateway.value).toBe(PaymentGateway.Stripe)
    expect(form.transactionId.value).toBe('')
    expect(form.amount.value).toBe(0)
    expect(form.fees.value).toBe(0)
    expect(form.submitting.value).toBe(false)
    expect(form.feedback.message.value).toBe('Recorded.')
    expect(form.feedbackTone.value).toBe(UiTone.Success)
  })

  it('shows the refusal sentence and keeps the fields when the payment is refused', async () => {
    const form = mountForm(optionsFor(false).options)
    fill(form)
    form.transactionId.value = 'ref-1'

    expect(await form.submit(7)).toBe(false)

    expect(form.feedback.message.value).toBe('The amount exceeds the balance.')
    expect(form.feedbackTone.value).toBe(UiTone.Danger)
    expect(form.amount.value).toBe(25)
    expect(form.transactionId.value).toBe('ref-1')
    expect(form.submitting.value).toBe(false)
  })

  it('falls back to the no-answer sentence when the refusal has none', async () => {
    const form = mountForm(optionsFor(false, { failure: () => null }).options)
    fill(form)

    await form.submit(7)

    expect(form.feedback.message.value).toBe('Could not reach the server. Check your connection and try again.')
  })
})
