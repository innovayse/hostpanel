/**
 * Unit tests for ProductsTestimonials: three testimonials read from the storefront store, each
 * with its author, quotes cut to 120 characters, and the block hiding itself when there are none.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ProductsTestimonials from '~/components/catalog/ProductsTestimonials.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import type { Testimonial } from '~/types/testimonial'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the testimonials it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these testimonials.
 *
 * @param testimonials The testimonials the public read answers with.
 */
const serve = (testimonials: Testimonial[]): void => {
  replies[readOf(StorefrontPath.Testimonials)] = { body: testimonials }
}

/**
 * Mounts the block with the translation component stood in for, and lets the testimonials load.
 *
 * @returns The wrapper.
 */
const mountBlock = () =>
  mountAfterReads(stub, [StorefrontPath.Testimonials], () =>
    mountWithKit(ProductsTestimonials, { components: { MarketingSectionHeader }, stubs: { 'i18n-t': true } }))

describe('ProductsTestimonials', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([1, 2, 3, 4].map(id => testimonialFixture(id)))
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows the first three testimonials with their author and company', async () => {
    const wrapper = await mountBlock()

    const cards = wrapper.findAll('[data-testid^="product-testimonial-"]')
    expect(cards).toHaveLength(3)
    expect(cards[0]?.text()).toContain('Example Person 1')
    expect(cards[0]?.text()).toContain('Example Co')
  })

  it('cuts a long quote to 120 characters and an ellipsis', async () => {
    serve([testimonialFixture(1, { quote: 'x'.repeat(200) })])

    const wrapper = await mountBlock()

    const quote = wrapper.find('[data-testid^="product-testimonial-"]').text()
    expect(quote).toContain(`"${'x'.repeat(120)}..."`)
    expect(quote).not.toContain('x'.repeat(121))
  })

  it('keeps a short quote whole', async () => {
    const wrapper = await mountBlock()

    expect(wrapper.find('[data-testid^="product-testimonial-"]').text()).toContain('"Example quote 1."')
  })

  it('renders nothing when there are no testimonials', async () => {
    serve([])

    const wrapper = await mountBlock()

    expect(wrapper.find('[data-testid="products-testimonials"]').exists()).toBe(false)
  })
})
