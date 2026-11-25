/**
 * Smoke tests for CurrenciesView: the exchange rate source choice, the table, the edit dialog, "make base" with its confirmation,
 * the exchange-rate refresh and the product-price picker, all through the store at network level.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import CurrenciesView from '@/views/CurrenciesView.vue'
import { ExchangeRateProvider } from '@/types/currency'
import type { CurrencyDto } from '@/types/currency'
import { BillingSettingKey } from '@/types/setting'
import { json, problem, stubFetch } from '../helpers/fetch'
import { mountScreen, mountWithApp } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { flushPromises } from '@vue/test-utils'
import { pickOption } from '../helpers/select'
import { useToastStore } from '@/stores/toast'
import { toastMessages } from '../helpers/toasts'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'

/** The base currency. */
const usd: CurrencyDto = {
  code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, rateToBase: 1, isBase: true, isEnabled: true,
}

/** A non-base currency. */
const eur: CurrencyDto = { ...usd, code: 'EUR', numeric: '978', prefix: '€', rateToBase: 0.92, isBase: false }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('CurrenciesView', () => {
  it('lists the currencies with their format sample and rate', async () => {
    stubRouteTable({ 'GET /admin/currencies': () => json([usd, eur]) })
    const { wrapper } = await mountScreen(CurrenciesView)

    expect(wrapper.get('h1').text()).toBe('Currencies')
    expect(wrapper.get('[data-testid="currency-row-USD"]').text()).toContain('$2.99')
    expect(wrapper.get('[data-testid="currency-row-EUR"]').text()).toContain('0.92000000')
    expect(wrapper.get('[data-testid="currency-row-USD"]').text()).toContain('Base')
  })

  it('draws the enabled state as a labelled badge', async () => {
    stubRouteTable({ 'GET /admin/currencies': () => json([usd, { ...eur, isEnabled: false }]) })
    const { wrapper } = await mountScreen(CurrenciesView)

    expect(wrapper.get('[data-testid="currency-row-USD"]').getComponent(UiStatusBadge).text()).toBe('Enabled')
    expect(wrapper.get('[data-testid="currency-row-EUR"]').getComponent(UiStatusBadge).text()).toBe('Disabled')
  })

  it('shows which bank the rates come from and saves another choice through the settings row', async () => {
    const mock = stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'GET /admin/settings': () => json([{ id: 7, key: BillingSettingKey.RateProvider, value: ExchangeRateProvider.Cba }]),
      'PUT /admin/settings/7': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(CurrenciesView)
    await flushPromises()

    expect(wrapper.get('[data-testid="exchange-rate-provider-select"]').text()).toContain('Central Bank of Armenia')

    await pickOption(wrapper, 'exchange-rate-provider-select', 'European Central Bank')
    await wrapper.get('[data-testid="exchange-rate-provider-save"]').trigger('click')
    await flushPromises()

    expect(requestsOf(mock)).toContain('PUT /admin/settings/7')
  })

  it('says the rate source row is not seeded instead of hiding the card', async () => {
    stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'GET /admin/settings': () => json([]),
    })
    const { wrapper } = await mountScreen(CurrenciesView)
    await flushPromises()

    expect(wrapper.find('[data-testid="exchange-rate-provider-not-seeded"]').exists()).toBe(true)
  })

  it('does not claim the rate source row is missing before the settings have loaded', async () => {
    stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'GET /admin/settings': () => json([]),
    })
    const wrapper = mountWithApp(CurrenciesView)

    // The first render comes before the load starts: no card yet, so no "not seeded" claim.
    expect(wrapper.find('[data-testid="exchange-rate-provider-not-seeded"]').exists()).toBe(false)

    await flushPromises()
    expect(wrapper.find('[data-testid="exchange-rate-provider-not-seeded"]').exists()).toBe(true)
  })

  it('shows the refusal of the settings load with a retry instead of hiding the card', async () => {
    stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'GET /admin/settings': () => problem('Settings are unavailable.', 503),
    })
    const { wrapper } = await mountScreen(CurrenciesView)
    await flushPromises()

    expect(wrapper.get('[data-testid="currencies-settings-error"]').text()).toContain('Settings are unavailable.')
    expect(wrapper.find('[data-testid="exchange-rate-provider"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="currencies-settings-error-retry"]').exists()).toBe(true)
  })

  it('opens the edit dialog with the stored values and closes it again', async () => {
    stubRouteTable({ 'GET /admin/currencies': () => json([usd, eur]) })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currency-actions-EUR"]').trigger('click')
    await wrapper.get('[data-testid="currency-edit-EUR"]').trigger('click')
    expect((wrapper.get('[data-testid="currency-form-prefix"]').element as HTMLInputElement).value).toBe('€')

    await wrapper.get('[data-testid="currency-form-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="currency-form"]').exists()).toBe(false)
  })

  it('saves an edit through PUT and closes the dialog', async () => {
    const mock = stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'PUT /admin/currencies/EUR': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currency-actions-EUR"]').trigger('click')
    await wrapper.get('[data-testid="currency-edit-EUR"]').trigger('click')
    await wrapper.get('[data-testid="currency-form"]').trigger('submit')
    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="currency-form"]').exists()).toBe(false)
    })

    expect(requestsOf(mock)).toContain('PUT /admin/currencies/EUR')
  })

  it('asks before making a currency the base, then reports it', async () => {
    const mock = stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'POST /admin/currencies/EUR/make-base': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currency-actions-EUR"]').trigger('click')
    await wrapper.get('[data-testid="currency-make-base-EUR"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Make EUR the base currency?')
    expect(requestsOf(mock)).not.toContain('POST /admin/currencies/EUR/make-base')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['EUR is now the base currency.'])
    })
  })

  it('offers the base currency only its edit action, as one icon button', async () => {
    stubRouteTable({ 'GET /admin/currencies': () => json([usd, eur]) })
    const { wrapper } = await mountScreen(CurrenciesView)

    expect(wrapper.find('[data-testid="currency-actions-USD"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="currency-edit-USD"]').find('svg').exists()).toBe(true)
    expect(wrapper.find('[data-testid="currency-make-base-USD"]').exists()).toBe(false)
  })

  it('refreshes the rates and names the codes that changed', async () => {
    stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'POST /admin/currencies/update-rates': () => json({ updated: ['EUR'], missing: ['AMD'] }),
    })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currencies-update-rates"]').trigger('click')

    await vi.waitFor(() => {
      expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Rates updated: EUR; not quoted: AMD'])
    })
  })

  it('shows a refusal in the result banner', async () => {
    stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'POST /admin/currencies/update-rates': () => problem('The rate source did not answer.', 502),
    })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currencies-update-rates"]').trigger('click')

    await vi.waitFor(() => {
      expect(toastMessages()).toContain('The rate source did not answer.')
    })
  })

  it('walks the price recompute: pick currencies, confirm the overwrite, report the count', async () => {
    const mock = stubRouteTable({
      'GET /admin/currencies': () => json([usd, eur]),
      'POST /admin/currencies/update-product-prices': () => json(4),
    })
    const { wrapper } = await mountScreen(CurrenciesView)

    await wrapper.get('[data-testid="currencies-update-prices"]').trigger('click')
    expect(wrapper.get('[data-testid="currencies-prices-continue"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="currencies-price-EUR"]').trigger('click')
    await wrapper.get('[data-testid="currencies-prices-continue"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('EUR')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['4 product price rows recomputed.'])
    })
    expect(requestsOf(mock)).toContain('POST /admin/currencies/update-product-prices')
  })

  it('says so when no currency is configured and shows a load refusal', async () => {
    stubRouteTable({ 'GET /admin/currencies': () => json([]) })
    const empty = await mountScreen(CurrenciesView)
    expect(empty.wrapper.text()).toContain('No currencies configured yet')

    stubRouteTable({ 'GET /admin/currencies': () => problem('Currencies are unavailable.', 503) })
    const failed = await mountScreen(CurrenciesView)
    expect(failed.wrapper.get('[data-testid="currencies-error"]').text()).toBe('Currencies are unavailable.')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = (await mountScreen(CurrenciesView)).wrapper
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="currencies-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
