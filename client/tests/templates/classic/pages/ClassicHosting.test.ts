/**
 * Unit tests for ClassicHosting: the loading and error branches, the free, grid and agency
 * plans, the billing-cycle tabs, the cart actions and the comparison table.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref, watch } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { at } from '~/tests/helpers/at'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { balancedColumns } from '~/utils/balancedGrid'
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import ClassicHosting from '~/templates/classic/pages/ClassicHosting.vue'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import type { BillingCurrency } from '~/types/currency'

/** The mocked catalogue endpoints, re-created for every test. */
const api = { fetchProducts: vi.fn() }

/** The currency the stood-in currency endpoint answers with. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

vi.mock('~/composables/apis/useCatalogApi', () => ({
  /**
   * Stands in for the catalogue API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useCatalogApi: () => api
}))

vi.mock('~/composables/apis/useCurrencyApi', () => ({
  /**
   * Stands in for the currency API composable.
   *
   * @returns Only the endpoint the page's store reaches for.
   */
  useCurrencyApi: () => ({ loadBillingCurrencies: () => Promise.resolve([usd]) })
}))

/** The plans the catalogue answers with. */
const plans = [
  { id: 1, name: 'Free', description: 'Start for nothing<br />\u2714 1 Website<br />\u2714 5 GB Bandwidth', pricing: { monthly: 0, annual: 0 } },
  { id: 2, name: 'Starter', description: 'Small sites<br />\u2714 5 GB SSD<br />\u2714 10 Emails', isFeatured: true, pricing: { monthly: 5, annual: 50 } },
  { id: 3, name: 'Business', description: 'Busy sites<br />\u2714 20 GB SSD', pricing: { monthly: 15, annual: null } },
  { id: 4, name: 'Agency', description: 'Many clients<br />\u2714 Unlimited Websites', pricing: { monthly: 40, annual: 400 } },
]

/**
 * Stands in for `useAsyncData` the way Nuxt runs it: the handler once, and again whenever a watched
 * source or a getter key changes - which is what re-reads the plans once the payer currency resolves.
 *
 * @param key The cache key; a getter is watched like a source.
 * @param handler The read to run.
 * @param options The `watch` sources to re-run on.
 * @returns The refs a page destructures.
 */
const runAsyncData = async (key: string | (() => string), handler: () => Promise<unknown>, options: { watch?: Array<() => unknown> } = {}) => {
  await handler()
  for (const source of [...(options.watch ?? []), ...(typeof key === 'function' ? [key] : [])]) {
    watch(source, () => handler())
  }
  return { data: ref(null), error: ref(null), pending: ref(false), refresh: handler }
}

/**
 * Mounts the page and lets the currencies load.
 *
 * @param props The page's props: the plan cards and comparison rows.
 * @returns The wrapper.
 */
const mountHosting = async (props: Record<string, unknown> = {}) => {
  const wrapper = await mountPage(ClassicHosting, { props })
  await flushPromises()
  return wrapper
}

describe('ClassicHosting', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
    vi.stubGlobal('useAsyncData', runAsyncData)
    api.fetchProducts.mockReset()
    api.fetchProducts.mockResolvedValue(plans)
    window.localStorage.clear()
  })

  it('reads the hosting group, and again in the payer currency once it resolves', async () => {
    await mountHosting()

    expect(api.fetchProducts).toHaveBeenLastCalledWith(expect.objectContaining({ gid: HOSTING_GROUP_ID, currency: 'USD' }))
  })

  it('renders the free plan in its own banner', async () => {
    const wrapper = await mountHosting()

    const free = wrapper.find('[data-testid="classic-hosting-free"]')
    expect(free.exists()).toBe(true)
    expect(free.text()).toContain('Free')
    expect(free.text()).toContain('1 Website')
  })

  it('renders every other plan except the agency one as a card, with the featured badge', async () => {
    const wrapper = await mountHosting()

    const cards = wrapper.findAll('[data-testid="classic-hosting-plan"]')
    expect(cards).toHaveLength(2)
    expect(at(cards, 0).text()).toContain('Starter')
    expect(at(cards, 0).find('[data-testid="classic-hosting-recommended"]').text()).toBe('hosting.recommended')
    expect(at(cards, 1).text()).toContain('Business')
    expect(at(cards, 1).find('[data-testid="classic-hosting-recommended"]').exists()).toBe(false)
  })

  it('shows the monthly price first and switches to the annual one from the tab', async () => {
    const wrapper = await mountHosting()
    expect(at(wrapper.findAll('[data-testid="classic-hosting-price"]'), 0).text()).toContain('5.00')

    await wrapper.find('[data-testid="classic-hosting-cycle-annual"]').trigger('click')

    expect(at(wrapper.findAll('[data-testid="classic-hosting-price"]'), 0).text()).toContain('50.00')
  })

  it('shows the agency plan in its own section', async () => {
    const wrapper = await mountHosting()

    const agency = wrapper.find('[data-testid="classic-hosting-agency"]')
    expect(agency.exists()).toBe(true)
    expect(agency.text()).toContain('Agency')
    expect(agency.text()).toContain('40.00')
  })

  it('adds a plan to the cart at the selected cycle', async () => {
    const wrapper = await mountHosting()
    await at(wrapper.findAll('[data-testid="classic-hosting-add"]'), 0).trigger('click')

    const item = useCartStore().items[0]
    expect(item).toMatchObject({ pid: 2, name: 'Starter', billingcycle: 'monthly', amount: 5, currency: 'USD' })
  })

  it('marks a plan that is already in the cart', async () => {
    const wrapper = await mountHosting()
    await at(wrapper.findAll('[data-testid="classic-hosting-add"]'), 0).trigger('click')

    expect(at(wrapper.findAll('[data-testid="classic-hosting-add"]'), 0).text()).toBe('hosting.inCart')
  })

  it('adds the agency plan from its section', async () => {
    const wrapper = await mountHosting()

    await wrapper.find('[data-testid="classic-hosting-agency-add"]').trigger('click')

    expect(useCartStore().items[0]).toMatchObject({ pid: 4, amount: 40 })
  })

  it('does not add a plan that has no price for the selected cycle', async () => {
    const wrapper = await mountHosting()
    await wrapper.find('[data-testid="classic-hosting-cycle-annual"]').trigger('click')

    await at(wrapper.findAll('[data-testid="classic-hosting-add"]'), 1).trigger('click')

    expect(useCartStore().items).toEqual([])
  })

  it('aligns the shared comparison rows to the page columns and marks a missing line as not included', async () => {
    const cards = [{ id: 2, name: 'Starter' }, { id: 3, name: 'Business' }]
    const comparisonRows = [
      { label: 'Disk', values: ['5 GB', '20 GB'] },
      { label: 'Emails', values: ['10', '50'] },
      { label: 'Backups', values: ['', 'Daily'] },
    ]
    const wrapper = await mountHosting({ plans: cards, comparisonRows })

    const table = wrapper.find('[data-testid="classic-hosting-compare"]')
    const headings = table.findAll('thead th').map((cell) => cell.text())
    expect(headings[1]).toContain('Starter')
    expect(headings[2]).toContain('Business')
    expect(table.find('[data-testid="classic-hosting-compare-row-Disk"]').text()).toContain('20 GB')
    expect(table.find('[data-testid="classic-hosting-compare-row-Backups"]').text()).toContain('hosting.comparison.notIncluded')
  })

  it('hides the comparison table when the plans share too few lines', async () => {
    const wrapper = await mountHosting({ plans: [{ id: 2, name: 'Starter' }], comparisonRows: [{ label: 'Disk', values: ['5 GB'] }] })

    expect(wrapper.find('[data-testid="classic-hosting-compare"]').exists()).toBe(false)
  })

  it.each([
    ['plans with zero features', []],
    ['plans with disjoint labels', [
      { label: 'Disk', values: ['5 GB', ''] },
      { label: 'Emails', values: ['', '50'] },
      { label: 'Backups', values: ['', 'Daily'] },
    ]],
  ])('renders no comparison table for %s', async (_name, comparisonRows) => {
    const wrapper = await mountHosting({ plans: [{ id: 2, name: 'Starter' }, { id: 3, name: 'Business' }], comparisonRows })

    expect(wrapper.find('[data-testid="classic-hosting-compare"]').exists()).toBe(false)
  })

  it('shows the failure sentence with a retry that reads again', async () => {
    api.fetchProducts.mockRejectedValue({ data: { detail: 'Catalogue unavailable.' } })
    const wrapper = await mountHosting()
    expect(wrapper.find('[data-testid="classic-hosting-error"]').exists()).toBe(true)

    api.fetchProducts.mockResolvedValue(plans)
    const before = api.fetchProducts.mock.calls.length
    await wrapper.find('[data-testid="classic-hosting-retry"]').trigger('click')
    await flushPromises()

    expect(api.fetchProducts).toHaveBeenCalledTimes(before + 1)
    expect(wrapper.find('[data-testid="classic-hosting-error"]').exists()).toBe(false)
  })

  it('renders the hero heading', async () => {
    const wrapper = await mountHosting()

    expect(wrapper.find('[data-testid="classic-hosting-title"]').text()).toContain('hosting.title')
  })

  it('lays the other plans out in a balanced grid, so four plans are 4 in a row and not 3 + 1', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 2, name: 'Starter', description: 'a', pricing: { monthly: 5, annual: 50 } },
      { id: 3, name: 'Business', description: 'b', pricing: { monthly: 15, annual: 150 } },
      { id: 5, name: 'Pro', description: 'c', pricing: { monthly: 25, annual: 250 } },
      { id: 6, name: 'Plus', description: 'd', pricing: { monthly: 35, annual: 350 } },
    ])
    const wrapper = await mountHosting()

    const grid = wrapper.find('[data-testid="classic-hosting-plans"]')
    expect(grid.classes()).toContain('ui-balanced-grid')
    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(4)))
    expect(grid.findAll('[data-testid="classic-hosting-plan"]')).toHaveLength(4)
  })

  it('has the payer currency loaded for pricing', async () => {
    await mountHosting()

    expect(useCurrencyStore().payerCode).toBe('USD')
  })

  it('uses classic tokens only: no gradients, blur glows or aurora colour families', async () => {
    const wrapper = await mountHosting()
    const html = wrapper.find('[data-testid="classic-hosting"]').html()

    expect(html).not.toMatch(/bg-gradient|blur-\[|animate-blob|shadow-halo|cyan-|violet-|secondary-/)
    expect(html).toContain('classic-')
  })
})
