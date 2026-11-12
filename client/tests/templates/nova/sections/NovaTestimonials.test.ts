/**
 * Unit tests for NovaTestimonials: nothing without quotes, one card per quote read from the
 * storefront store with them.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import NovaTestimonials from '~/templates/nova/sections/NovaTestimonials.vue'
import type { Testimonial } from '~/types/testimonial'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the quotes it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these quotes.
 *
 * @param testimonials The testimonials the public read answers with.
 */
const serve = (testimonials: Testimonial[]): void => {
  replies[readOf(StorefrontPath.Testimonials)] = { body: testimonials }
}

/**
 * Mounts the section and lets the quotes load.
 *
 * @returns The wrapper.
 */
const mountSection = () =>
  mountAfterReads(stub, [StorefrontPath.Testimonials], () => mountWithKit(NovaTestimonials))

describe('NovaTestimonials', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([
      testimonialFixture(1, { name: 'Ada', role: 'CTO', company: 'Example Co', quote: 'Fast.' }),
      testimonialFixture(2, { name: 'Bo', role: null, company: null, quote: 'Solid.' }),
    ])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders no section while there are no quotes', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.find('li').exists()).toBe(false)
  })

  it('renders each quote with its attribution', async () => {
    const cards = (await mountSection()).findAll('li')

    expect(cards).toHaveLength(2)
    expect(cards[0]!.find('blockquote').text()).toBe('Fast.')
    expect(cards[0]!.text()).toContain('CTO, Example Co')
    expect(cards[1]!.text()).toContain('Bo')
    expect(cards[1]!.text()).not.toContain('CTO')
  })

  it('keeps cards at a normal column width, centred, instead of stretching them', async () => {
    const list = (await mountSection()).find('ul')

    expect(list.classes()).toContain('justify-center')
    expect(list.find('li').classes()).toContain('lg:w-[calc((100%-2.5rem)/3)]')
  })
})
