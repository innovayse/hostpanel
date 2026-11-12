/**
 * Unit tests for CurrencySwitcher: the current currency on the trigger, the menu of enabled
 * currencies, choosing one (store and persistence), hiding when there is no choice, and the
 * read-only chip with a hint for a signed-in client whose currency is fixed.
 *
 * @module tests/components/ui/UiCurrencySwitcher.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { settle } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import UiCurrencySwitcher from '~/components/ui/UiCurrencySwitcher.vue'
import { useClientStore } from '~/stores/client'
import type { BillingCurrency } from '~/types/currency'
import { UiMenuAlign, UiPlacement } from '~/utils/ui/enums'

/** Path of the endpoint that lists the enabled billing currencies. */
const CURRENCIES_PATH = '/api/portal/public/billing-currencies'

/** The stub playing the BFF. */
let stub: StubServer

/** The currency a signed-in client is fixed to, or null for a guest; set per test. */
let fixedCurrency: string | null = null

/** The currencies the stubbed endpoint answers with, set per test. */
let currencies: BillingCurrency[] = []

/** USD, the base currency. */
const USD: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

/** AMD, an additional enabled currency. */
const AMD: BillingCurrency = { code: 'AMD', numeric: '051', prefix: '', suffix: ' ֏', decimals: 0, isBase: false }

/**
 * Mounts the switcher in a Pinia that holds the signed-in client, if any, and waits for the mounted
 * load to be answered by the stub and applied.
 *
 * @param props Props passed to the component.
 * @returns The wrapper, with the currency store filled.
 */
const mountSwitcher = async (props: Record<string, unknown> = {}) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  if (fixedCurrency !== null) {
    useClientStore().user = makeClientUser({ currency: fixedCurrency })
  }
  const wrapper = mountWithKit(UiCurrencySwitcher, { props, pinia })
  await vi.waitFor(() => expect(requestsTo(stub.requests, CURRENCIES_PATH)).toHaveLength(1))
  await settle(5)
  return wrapper
}

describe('UiCurrencySwitcher', () => {
  beforeEach(async () => {
    fixedCurrency = null
    currencies = [USD, AMD]
    localStorage.clear()
    stub = await startTransport(routeTable({ [`GET ${CURRENCIES_PATH}`]: () => ({ body: currencies }) }))
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
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
    currencies = [USD]

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher"]').exists()).toBe(false)
  })

  it('renders nothing before any currency has loaded', async () => {
    currencies = []

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher"]').exists()).toBe(false)
  })

  it('shows a signed-in client fixed currency read-only, with a hint and no menu', async () => {
    fixedCurrency = 'AMD'

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-trigger"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="currency-switcher-current"]').text()).toBe('AMD')
    expect(wrapper.find('[data-testid="currency-switcher-fixed"]').attributes('title')).toBe('ui.currencyFixed')

    await wrapper.find('[data-testid="ui-tooltip-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="currency-switcher-hint"]').text()).toBe('ui.currencyFixed')
  })

  it('shows a fixed currency even when only one currency is enabled', async () => {
    fixedCurrency = 'USD'
    currencies = [USD]

    const wrapper = await mountSwitcher()

    expect(wrapper.find('[data-testid="currency-switcher-fixed"]').exists()).toBe(true)
  })
})
