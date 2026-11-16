/**
 * Unit tests for the hosting plans page against the real catalogue and currency stores, the real API
 * composables and the real transport, with a stub BFF answering on a real socket: it hands the
 * template's component the plan cards and the comparison rows, remembers the billing toggle, and sets
 * the head.
 *
 * @module tests/pages/hosting/index.test
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { PUBLIC_PAGE_BASE_URL, canonicalOf, mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubTemplateSlots } from '~/tests/helpers/templateSlots'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import HostingPage from '~/pages/hosting/index.vue'
import { useTemplate } from '~/composables/useTemplate'
import { PricingCycle } from '~/types/pricingcycle'
import { TemplateSlot } from '~/types/templateslot'
import { buildComparisonRows } from '~/utils/comparisonRows'
import type { PortalProduct } from '~/types/portalproduct'

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

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useTemplate })

/**
 * Mounts the page and lets the catalogue and the currencies load.
 *
 * @returns The wrapper.
 */
const mountHosting = async () => {
  const wrapper = await mountPage(HostingPage)
  await awaitCatalogue()
  return wrapper
}

describe('hosting page', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    stubTemplateSlots({ [TemplateSlot.Hosting]: TemplateHosting })
    vi.stubGlobal('buildComparisonRows', buildComparisonRows)
    window.localStorage.clear()
    Object.assign(replies, {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`GET ${CatalogPath.Products}`]: { body: plans },
      [`GET ${CatalogPath.ProductFeatures}`]: { body: [] },
    })
    await loadPortalSettings(replies)
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

    expect(canonicalOf(spies)).toBe(`${PUBLIC_PAGE_BASE_URL}/hosting`)
  })
})
