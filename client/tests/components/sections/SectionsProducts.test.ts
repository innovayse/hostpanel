/**
 * Unit tests for SectionsProducts: the first three cards (the static domains card, then the
 * catalogue families, featured first) in a grid that keeps three column card widths, each painted
 * in its own colour, with a learn-more call to action and a link to every plan.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { ProductFeature } from '~/types/productfeature'
import SectionsProducts from '~/components/sections/SectionsProducts.vue'
import ProductFamilyCard from '~/components/catalog/ProductFamilyCard.vue'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { HomeSectionId } from '~/types/homesectionid'
import { HOME_PRODUCT_FAMILY_LIMIT } from '~/constants/homeProducts'
import { ProductType } from '~/types/producttype'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change what it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these products and groups.
 *
 * @param products The products the public read answers with.
 * @param productGroups The groups the public read answers with.
 */
const serve = (products: PortalProduct[], productGroups: PortalProductGroup[]): void => {
  replies[`GET ${CatalogPath.Products}`] = { body: products }
  replies[`GET ${CatalogPath.ProductGroups}`] = { body: productGroups }
}

/**
 * The specification lines of one group the stub serves; every other group has none.
 *
 * @param groupId The group whose lines are served.
 * @param lines The specification lines.
 */
const serveFeatures = (groupId: number, lines: ProductFeature[]): void => {
  replies[`GET ${CatalogPath.ProductFeatures}`] = (request) => ({ body: queryOf(request).gid === String(groupId) ? lines : [] })
}

/** One hosting plan and one SaaS family with its parent. */
const catalogue: PortalProduct[] = [
  { id: 1, groupId: 1, name: 'Web Hosting', slug: 'web-hosting', type: ProductType.SharedHosting, description: 'Fast hosting<br />✔ SSD<br />✔ SSL<br />✔ Backups<br />✔ Email' },
  { id: 10, groupId: 3, name: 'Alpha Suite', slug: 'alpha-suite', type: ProductType.Vps, description: 'Learn online<br />✔ Courses' },
  { id: 20, groupId: 6, name: 'Domain Registration', slug: 'domain-registration', type: ProductType.Domain, description: 'Register domains' },
  { id: 30, groupId: 7, name: 'Plain Plan', slug: 'plain-plan', type: ProductType.Other, description: 'No bullets here' },
]

/** The groups: hosting and one family are shown; one is empty and one is switched off. */
const groups: PortalProductGroup[] = [
  { id: 3, name: 'Alpha Suite', productCount: 1 },
  { id: 1, name: 'Web Hosting', productCount: 1 },
  { id: 6, name: 'Domains', productCount: 1 },
  { id: 7, name: 'Plain Plan', productCount: 1 },
  { id: 4, name: 'Beta Kit', productCount: 0 },
]

/**
 * Mounts the section and lets the catalogue load.
 *
 * @returns The wrapper.
 */
const mountSection = async () => {
  const wrapper = await mountPage(SectionsProducts, { components: { ...CLASSIC_COMPONENTS, CatalogProductFamilyCard: ProductFamilyCard } })
  // The catalogue crosses a real socket, and the section renders nothing until its reads are done.
  await vi.waitFor(() => expect(wrapper.find('[data-testid="sections-products"]').exists()).toBe(true))
  await flushPromises()
  return wrapper
}

/**
 * Makes the catalogue serve everything except the given group, so a family further down the list
 * moves into the first three cards.
 *
 * @param groupId The group to leave out.
 */
const withoutGroup = (groupId: number): void => {
  serve(catalogue.filter((item) => item.groupId !== groupId), groups.filter((item) => item.id !== groupId))
}

describe('SectionsProducts', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubPublicPageRuntime()
    serve(catalogue, groups)
    replies[`GET ${CatalogPath.ProductFeatures}`] = { body: [] }
    replies[`GET ${CatalogPath.BillingCurrencies}`] = { body: [] }
    window.localStorage.clear()
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('carries the services anchor the header links to, with room above it', async () => {
    const section = (await mountSection()).find('[data-testid="sections-products"]')

    expect(section.attributes('id')).toBe(HomeSectionId.Services)
    expect(section.classes()).toContain(HOME_SECTION_SCROLL_MARGIN_CLASS)
  })

  it('starts with the domains card, which links to the domains page', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid^="product-card-"]').attributes('data-testid')).toBe('product-card-domains')
    expect(wrapper.find('[data-testid="product-link-domains"]').attributes('href')).toBe('/en/domains')
  })

  it('shows only the first three cards and leaves out empty groups', async () => {
    const wrapper = await mountSection()

    expect(wrapper.findAll('[data-testid^="product-card-"]')).toHaveLength(HOME_PRODUCT_FAMILY_LIMIT)
    expect(wrapper.find('[data-testid="product-card-web-hosting"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="product-card-alpha-suite"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="product-card-plain-plan"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="product-card-beta-kit"]').exists()).toBe(false)
  })

  it('puts a featured family first and keeps the grid at three column widths', async () => {
    serve(catalogue.map((item) => (item.id === 30 ? { ...item, isFeatured: true } : item)), groups)
    const wrapper = await mountSection()

    const ids = wrapper.findAll('[data-testid^="product-card-"]').map((card) => card.attributes('data-testid'))
    expect(ids[0]).toBe('product-card-plain-plan')
    expect(ids).toHaveLength(3)
    expect(wrapper.find('[data-testid="home-products-grid"]').attributes('data-columns')).toBe('3')
  })

  it('keeps a single card at a column width instead of stretching it', async () => {
    serve(catalogue, [])
    const wrapper = await mountSection()

    const grid = wrapper.find('[data-testid="home-products-grid"]')
    expect(grid.attributes('data-columns')).toBe('3')
    expect(grid.classes()).toContain('justify-center')
    expect(grid.classes()).not.toContain('lg:[&>*]:basis-full')
  })

  it('reads specification lines only for the families it shows', async () => {
    await mountSection()

    const asked = requestsTo(stub.requests, CatalogPath.ProductFeatures).map((request) => Number(queryOf(request).gid)).sort()
    expect(asked).toEqual([1, 3])
  })

  it('puts hosting before the other families and asks only for the family groups products', async () => {
    serve(catalogue.filter((item) => item.groupId !== 6 && item.groupId !== 7), groups.filter((item) => item.id !== 6 && item.id !== 7))
    const wrapper = await mountSection()

    const ids = wrapper.findAll('[data-testid^="product-card-"]').map((card) => card.attributes('data-testid') ?? '')
    const cards = ids.filter((id) => ['domains', 'web-hosting', 'alpha-suite'].includes(id.replace('product-card-', '')))
    expect(cards).toEqual(['product-card-domains', 'product-card-web-hosting', 'product-card-alpha-suite'])
    const reads = requestsTo(stub.requests, CatalogPath.Products)
    expect(reads).toHaveLength(1)
    expect(queryOf(reads[0]!).gids).toBe('1,3')
  })

  it('reads no products and shows only the domains card when the catalogue has no family', async () => {
    serve(catalogue, [])
    const wrapper = await mountSection()

    expect(requestsTo(stub.requests, CatalogPath.Products)).toHaveLength(0)
    expect(wrapper.find('[data-testid="product-card-domains"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="product-card-web-hosting"]').exists()).toBe(false)
  })

  it('shows a family summary and at most three features', async () => {
    const wrapper = await mountSection()

    const hosting = wrapper.find('[data-testid="product-card-web-hosting"]')
    expect(hosting.text()).toContain('Fast hosting')
    expect(hosting.text()).toContain('Backups')
    expect(hosting.text()).not.toContain('Email')
  })

  it('shows domains once: the domains group is covered by the static card', async () => {
    const wrapper = await mountSection()

    expect(wrapper.findAll('[data-testid="product-card-domains"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="product-card-domain-registration"]').exists()).toBe(false)
  })

  it('draws each family tile from the type of its products with a full icon name', async () => {
    const wrapper = await mountSection()

    expect(wrapper.html()).not.toContain('mdi:mdi:')
    expect(wrapper.find('[data-testid="product-card-web-hosting"]').html()).toContain('mdi:cloud-check')
    expect(wrapper.find('[data-testid="product-card-alpha-suite"]').html()).toContain('mdi:server')
  })

  it('lists no features, and no placeholder, for a family with none', async () => {
    withoutGroup(3)
    const wrapper = await mountSection()

    const plain = wrapper.find('[data-testid="product-card-plain-plan"]')
    expect(plain.text()).not.toContain('SaaS')
    expect(plain.findAll('svg')).toHaveLength(0)
  })

  it('uses the specification lines when a description lists no features', async () => {
    withoutGroup(3)
    serveFeatures(7, [{ id: 1, productId: 30, label: 'Disk', value: '10 GB', sortOrder: 1 }])
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="product-card-plain-plan"]').text()).toContain('Disk: 10 GB')
  })

  it('links View all to the products page', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="home-products-all"]').attributes('href')).toBe('/en/products')
  })

  it('draws each card with the shared family card: a generated cover and the bullets always visible', async () => {
    const wrapper = await mountSection()

    const hosting = wrapper.find('[data-testid="product-card-web-hosting"]')
    expect(hosting.find('[data-testid="product-cover"]').exists()).toBe(true)
    expect(hosting.find('[data-testid="product-bullets"]').exists()).toBe(true)
    expect(hosting.html()).not.toContain('opacity-0 group-hover:opacity-100')
  })

  it('shows the Featured badge only on a family whose plan is recommended', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="product-card-domains"] [data-testid="product-featured"]').exists()).toBe(false)
  })
})
