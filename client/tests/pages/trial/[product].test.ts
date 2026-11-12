/**
 * Unit tests for the trial page against the real catalogue and currency stores, the real API
 * composables and the real transport, with a stub BFF answering on a real socket: it takes the
 * product's colour, icon and page from its catalogue group, reads its feature lines from the
 * catalogue, and falls back to a neutral look for an unknown slug.
 *
 * @module tests/pages/trial/[product].test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import TrialPage from '~/pages/trial/[product].vue'
import CatalogTrialHero from '~/components/catalog/TrialHero.vue'
import CatalogTrialDetails from '~/components/catalog/TrialDetails.vue'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { familyColor } from '~/utils/catalogGroups'

/** The product's group. */
const groups: PortalProductGroup[] = [{ id: 9, name: 'Gamma Tool', productCount: 1 }]

/** The product's catalogue entry. */
const catalogue: PortalProduct[] = [
  { id: 90, groupId: 9, name: 'Gamma Tool', description: 'Tasks<br />✔ Boards<br />✔ Reports' },
]

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the page for one product slug and waits until the catalogue has been read.
 *
 * @param product The `product` route parameter.
 * @returns The wrapper.
 */
const mountTrial = async (product = 'gamma-tool') => {
  spies = stubPublicPageRuntime({ params: { product } })
  const wrapper = await mountPage(TrialPage, { components: { CatalogTrialHero, CatalogTrialDetails } })
  await awaitCatalogue()
  return wrapper
}

describe('trial page', () => {
  beforeEach(async () => {
    window.localStorage.clear()
    table = {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [] },
      [`GET ${CatalogPath.ProductGroups}`]: { body: groups },
      [`GET ${CatalogPath.Products}`]: { body: catalogue },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it("reads the products of the family groups", async () => {
    await mountTrial()

    expect(requestsTo(stub.requests, CatalogPath.Products).map(queryOf)).toContainEqual(expect.objectContaining({ gids: '9' }))
  })

  it("paints the page in the product's colour and sends the trial to its page", async () => {
    const wrapper = await mountTrial()

    const accent = wrapper.find('[data-testid="ui-accent"]').element as HTMLElement
    expect(accent.style.getPropertyValue('--accent')).toBe(familyColor('gamma-tool'))
    expect(wrapper.find('[data-testid="trial-cta"]').attributes('href')).toBe('/products/gamma-tool')
  })

  it('lists the feature lines parsed from the catalogue description', async () => {
    const wrapper = await mountTrial()

    expect(wrapper.find('[data-testid="trial-features"]').text()).toContain('Boards')
    expect(wrapper.find('[data-testid="trial-features"]').text()).toContain('Reports')
  })

  it('shows the three trial perks', async () => {
    const wrapper = await mountTrial()

    expect(wrapper.findAll('[data-testid^="trial-perk-"]')).toHaveLength(3)
  })

  it('falls back to a neutral look and the products list for an unknown slug', async () => {
    table[`GET ${CatalogPath.ProductGroups}`] = { body: [] }
    table[`GET ${CatalogPath.Products}`] = { body: [] }
    const wrapper = await mountTrial('mystery')

    const accent = wrapper.find('[data-testid="ui-accent"]').element as HTMLElement
    // No colour of its own: no inline accent, the sections take the portal brand accent instead.
    expect(accent.style.getPropertyValue('--accent')).toBe('')
    expect(wrapper.find('[data-testid="trial-hero"]').classes().join(' ')).toContain('--brand-primary-500')
    expect(wrapper.find('[data-testid="trial-learn-more"]').attributes('href')).toBe('/en/products')
    expect(wrapper.find('[data-testid="trial-features"]').exists()).toBe(false)
  })

  it('sets the head for the product', async () => {
    await mountTrial()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ path: '/trial/gamma-tool' }))
  })
})
