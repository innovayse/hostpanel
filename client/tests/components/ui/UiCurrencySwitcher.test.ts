/**
 * Unit tests for CurrencySwitcher: the current currency on the trigger, the menu of enabled
 * currencies, choosing one (store and persistence), hiding when there is no choice, and the
 * read-only chip with a hint for a signed-in client whose currency is fixed.
 *
 * @module tests/components/ui/UiCurrencySwitcher.test
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import { flushPromises } from '@vue/test-utils'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiCurrencySwitcher from '~/components/ui/UiCurrencySwitcher.vue'
import { UiMenuAlign, UiPlacement } from '~/utils/ui/enums'

/** The mocked signed-in client, reset before each test. */
let mockUser: { currency?: string | null } | null = null

/** The currencies the stubbed endpoint answers with, set per test. */
let mockCurrencies: Array<Record<string, unknown>> = []

vi.mock('~/stores/client', () => ({
  /**
   * Stands in for the client store so the fixed-currency case needs no client-area state.
   *
   * @returns An object exposing only the `user` field the currency store reads.
   */
  useClientStore: () => ({ user: mockUser }),
}))

vi.mock('~/composables/apis/useCurrencyApi', () => ({
  /**
   * Stands in for the currency API composable.
   *
   * @returns An object whose loader answers the per-test currency list.
   */
  useCurrencyApi: () => ({ loadBillingCurrencies: () => Promise.resolve(mockCurrencies) }),
}))

/** USD, the base currency. */
const USD = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

/** AMD, an additional enabled currency. */
const AMD = { code: 'AMD', numeric: '051', prefix: '', suffix: ' ֏', decimals: 0, isBase: false }

/**
 * Mounts the switcher and waits for the mounted load to settle.
 *
 * @param props Props passed to the component.
 * @returns The wrapper, with the currency store filled.
 */
const mountSwitcher = async (props: Record<string, unknown> = {}) => {
  const wrapper = mountWithKit(UiCurrencySwitcher, { props, pinia: createPinia() })
  await flushPromises()
  return wrapper
}

describe('UiCurrencySwitcher', () => {
  beforeEach(() => {
    mockUser = null
    mockCurrencies = [USD, AMD]
    localStorage.clear()
  })

  it('shows the payer currency (the base one) on the trigger', async () => {
    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-current"]').text()).toBe('USD')
  })

  it('replaces the tinted frame with the class a screen supplies', async () => {
    const wrapper = await mountSwitcher({ triggerClass: 'h-10 rounded-xl border-line2' })
    const classes = wrapper.find('[data-testid="currency-switcher-trigger"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['h-10', 'rounded-xl', 'border-line2']))
    expect(classes).not.toContain('border-primary-500/30')
  })

  it('draws the trigger as a brand-tinted outline, not a grey fill', async () => {
    const wrapper = await mountSwitcher()
    const classes = wrapper.find('[data-testid="currency-switcher-trigger"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['border-primary-500/30', 'hover:bg-primary-500/10']))
    expect(classes).not.toContain('bg-gray-100')
  })

  it('keeps the menu closed until the trigger is clicked, then lists every enabled currency', async () => {
    const wrapper = await mountSwitcher()
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)

    await wrapper.find('[data-testid="currency-switcher-trigger"]').trigger('click')

    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(2)
    expect(wrapper.find('[data-testid="currency-option-AMD"]').text()).toContain('AMD')
    expect(wrapper.find('[data-testid="currency-switcher-trigger"]').attributes('aria-expanded')).toBe('true')
  })

  it('chooses a currency, persists it and closes the menu', async () => {
    const wrapper = await mountSwitcher()
    await wrapper.find('[data-testid="currency-switcher-trigger"]').trigger('click')

    await wrapper.find('[data-testid="currency-option-AMD"]').trigger('click')

    expect(wrapper.find('[data-testid="currency-switcher-current"]').text()).toBe('AMD')
    expect(localStorage.getItem('innovayse_currency')).toBe('AMD')
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('restores a persisted choice on mount', async () => {
    localStorage.setItem('innovayse_currency', 'AMD')

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-current"]').text()).toBe('AMD')
  })

  it('right-aligns the menu under the trigger by default', async () => {
    const wrapper = await mountSwitcher()

    await wrapper.find('[data-testid="currency-switcher-trigger"]').trigger('click')

    const menu = wrapper.find('[role="menu"]')
    expect(menu.classes()).toEqual(expect.arrayContaining(['absolute', 'right-0', 'max-w-[calc(100vw-2rem)]']))
    expect(menu.classes()).not.toContain('left-0')
  })

  it('left-aligns the menu when aligned to the start', async () => {
    const wrapper = await mountSwitcher({ align: UiMenuAlign.Start })

    await wrapper.find('[data-testid="currency-switcher-trigger"]').trigger('click')

    expect(wrapper.find('[role="menu"]').classes()).toContain('left-0')
  })

  it('opens upward when placed on top', async () => {
    const wrapper = await mountSwitcher({ placement: UiPlacement.Top })

    await wrapper.find('[data-testid="currency-switcher-trigger"]').trigger('click')

    expect(wrapper.find('[role="menu"]').classes()).toContain('bottom-full')
  })

  it('renders nothing for a guest when only one currency is enabled', async () => {
    mockCurrencies = [USD]

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher"]').exists()).toBe(false)
  })

  it('renders nothing before any currency has loaded', async () => {
    mockCurrencies = []

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher"]').exists()).toBe(false)
  })

  it('shows a signed-in client fixed currency read-only, with a hint and no menu', async () => {
    mockUser = { currency: 'AMD' }

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-trigger"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="currency-switcher-current"]').text()).toBe('AMD')
    expect(wrapper.find('[data-testid="currency-switcher-fixed"]').attributes('title')).toBe('ui.currencyFixed')

    await wrapper.find('[data-testid="ui-tooltip-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="currency-switcher-hint"]').text()).toBe('ui.currencyFixed')
  })

  it('shows a fixed currency even when only one currency is enabled', async () => {
    mockUser = { currency: 'USD' }
    mockCurrencies = [USD]

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-fixed"]').exists()).toBe(true)
  })
})
