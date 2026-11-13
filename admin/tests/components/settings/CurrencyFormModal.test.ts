/**
 * Tests for CurrencyFormModal: create and edit modes, the live format sample, the base currency's
 * locked fields, and the emitted payload.
 */
import { describe, expect, it } from 'vitest'
import CurrencyFormModal from '@/components/settings/CurrencyFormModal.vue'
import type { CurrencyDto } from '@/types/currency'
import { mountScreen } from '../../helpers/mount'

/** A non-base currency. */
const eur: CurrencyDto = {
  code: 'EUR', numeric: '978', prefix: '€', suffix: '', decimals: 2, rateToBase: 0.92, isBase: false, isEnabled: true,
}

/** The base currency. */
const usd: CurrencyDto = { ...eur, code: 'USD', numeric: '840', prefix: '$', rateToBase: 1, isBase: true }

/**
 * Reads the value of the native input behind a test id.
 *
 * @param wrapper - The mounted dialog.
 * @param testId - The `data-testid` of the field.
 * @returns The input's current value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper'], testId: string): string =>
  (wrapper.get(`[data-testid="${testId}"]`).element as HTMLInputElement).value

describe('CurrencyFormModal', () => {
  it('opens empty in create mode with an editable code', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: null, saving: false })

    expect(wrapper.get('h2').text()).toBe('Add Currency')
    expect(valueOf(wrapper, 'currency-form-code')).toBe('')
    expect(wrapper.get('[data-testid="currency-form-code"]').attributes('disabled')).toBeUndefined()
  })

  it('fills the fields from the edited currency and locks the code', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: eur, saving: false })

    expect(wrapper.get('h2').text()).toBe('Edit Currency')
    expect(valueOf(wrapper, 'currency-form-code')).toBe('EUR')
    expect(valueOf(wrapper, 'currency-form-prefix')).toBe('€')
    expect(wrapper.get('[data-testid="currency-form-code"]').attributes('disabled')).toBeDefined()
  })

  it('shows how an amount will render with the typed prefix', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: eur, saving: false })

    await wrapper.get('[data-testid="currency-form-prefix"]').setValue('EUR ')

    expect(wrapper.get('[data-testid="currency-form-sample"]').text()).toContain('EUR 2.99')
  })

  it('emits the upper-cased code and the payload on submit', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: null, saving: false })

    await wrapper.get('[data-testid="currency-form-code"]').setValue('gbp')
    await wrapper.get('[data-testid="currency-form-numeric"]').setValue('826')
    await wrapper.get('[data-testid="currency-form-prefix"]').setValue('£')
    await wrapper.get('[data-testid="currency-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      'GBP',
      { numeric: '826', prefix: '£', suffix: '', decimals: 2, rateToBase: 1, isEnabled: true },
    ]])
  })

  it('locks the rate and the enabled switch for the base currency', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: usd, saving: false })

    expect(wrapper.get('[data-testid="currency-form-enabled"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('always worth')
  })

  it('closes from Cancel and disables Save while saving', async () => {
    const { wrapper } = await mountScreen(CurrencyFormModal, { currency: eur, saving: true })

    expect(wrapper.get('[data-testid="currency-form-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="currency-form-save"]').text()).toBe('Save Changes')
    await wrapper.get('[data-testid="currency-form-cancel"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
