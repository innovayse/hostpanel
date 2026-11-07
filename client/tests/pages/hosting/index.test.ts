/**
 * Unit tests for the hosting plans page: it hands the template's component the plan cards and the
 * comparison rows, remembers the billing toggle, and sets the head.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import HostingPage from '~/pages/hosting/index.vue'
import { PricingCycle } from '~/types/pricingcycle'
import { buildComparisonRows } from '~/utils/comparisonRows'
import type { BillingCurrency } from '~/types/currency'
import type { PortalProduct } from '~/types/portalproduct'

/** The mocked catalogue endpoints, re-created for every test. */
const api = { fetchProducts: vi.fn(), fetchProductFeatures: vi.fn() }

/** The currency the stood-in currency endpoint answers with. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

vi.mock('~/composables/apis/useCatalogApi', () => ({
  /**
   * Stands in for the catalogue API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useCatalogApi: () => api,
}))

vi.mock('~/composables/apis/useCurrencyApi', () => ({
  /**
   * Stands in for the currency API composable.
   *
   * @returns Only the endpoint the page's store reaches for.
   */
  useCurrencyApi: () => ({ loadBillingCurrencies: () => Promise.resolve([usd]) }),
}))

/** The hosting plans the catalogue answers with. */
const plans: PortalProduct[] = [
  { id: 2, groupId: 1, name: 'Starter', description: 'Small sites', pricing: { monthly: 5, annual: 50 } },
  { id: 3, groupId: 1, name: 'Business', description: 'Busy sites', pricing: { monthly: 15, annual: 150 } },
]

/** What the template component is given, recorded for the assertions. */
const received = { props: {} as Record<string, unknown> }

/** Stand-in for the template's hosting component: records its props and offers a way to emit. */
const TemplateHosting = defineComponent({
  name: 'TemplateHosting',
  props: { plans: { type: Array, default: () => [] }, cycle: { type: String, default: 'monthly' }, comparisonRows: { type: Array, default: () => [] } },
  emits: ['update:cycle'],
  setup(props) {
    received.props = props
    return () => h('div', { 'data-testid': 'template-hosting' }, `${props.plans.length} plans, cycle ${props.cycle}`)
  },
})

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/**
 * Mounts the page and lets the catalogue and the currencies load.
 *
 * @returns The wrapper.
 */
const mountHosting = async () => {
  const wrapper = await mountPage(HostingPage)
  await flushPromises()
  return wrapper
}

describe('hosting page', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    vi.stubGlobal('useTemplate', () => ({ slot: () => TemplateHosting }))
    vi.stubGlobal('buildComparisonRows', buildComparisonRows)
    api.fetchProducts.mockReset()
    api.fetchProducts.mockResolvedValue(plans)
    api.fetchProductFeatures.mockReset()
    api.fetchProductFeatures.mockResolvedValue([])
    window.localStorage.clear()
  })

  it('renders the template component with one plan card per hosting plan', async () => {
    const wrapper = await mountHosting()

    expect(wrapper.find('[data-testid="template-hosting"]').text()).toContain('2 plans')
  })

  it('links every plan card to its configure page', async () => {
    await mountHosting()

    const cards = received.props.plans as Array<{ href: string }>
    expect(cards.map((card) => card.href)).toEqual(['/en/configure/2', '/en/configure/3'])
  })

  it('starts on monthly billing and follows the template when it toggles', async () => {
    const wrapper = await mountHosting()
    expect(received.props.cycle).toBe(PricingCycle.Monthly)

    wrapper.findComponent(TemplateHosting).vm.$emit('update:cycle', PricingCycle.Biennial)
    await flushPromises()

    expect(wrapper.find('[data-testid="template-hosting"]').text()).toContain('cycle biennial')
  })

  it('hands over the comparison rows built from the group features', async () => {
    await mountHosting()

    expect(Array.isArray(received.props.comparisonRows)).toBe(true)
  })

  it('sets the canonical head for the page', async () => {
    await mountHosting()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ path: '/hosting' }))
  })
})
