/**
 * Unit tests for AuroraTestimonials: the quote on show comes from the storefront store, the
 * previous and next arrows and the arrow keys wrap at both ends, the dots jump, each card shows
 * its rating, avatar or initials, role, company and localised date, and the section hides itself
 * when there are no quotes.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, readOf, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { Testimonial } from '~/types/testimonial'
import AuroraTestimonials from '~/templates/aurora/sections/AuroraTestimonials.vue'
import { stubAuroraGlobals } from '../auroraGlobals'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the quotes it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these quotes.
 *
 * @param testimonials The quotes the public read answers with.
 */
const serve = (testimonials: Testimonial[]): void => {
  replies[readOf(StorefrontPath.Testimonials)] = { body: testimonials }
}

/**
 * Mounts the section and lets the quotes load.
 *
 * @returns The wrapper.
 */
const mountSection = async () => {
  stubAuroraGlobals()
  return mountAfterReads(stub, [StorefrontPath.Testimonials], () => mountWithKit(AuroraTestimonials))
}

/**
 * Finds the quote card on show (the others are hidden but stay in the DOM).
 *
 * @param wrapper The mounted section.
 * @returns The visible card.
 */
const visibleCard = (wrapper: Awaited<ReturnType<typeof mountSection>>) =>
  wrapper.findAll('[data-testid="testimonial-card"]').find(card => !card.classes().includes('hidden'))!

describe('AuroraTestimonials', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([
      testimonialFixture(1, { quote: 'First quote', name: 'Ann Lee', role: 'CEO', company: 'Example Co', rating: 4, publishedAt: '2026-03-10T00:00:00+00:00' }),
      testimonialFixture(2, { quote: 'Second quote', name: 'Bob', role: null, company: null, avatarUrl: '/uploads/bob.png', publishedAt: '2026-05-02T00:00:00+00:00' }),
    ])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows the first quote with its author, role and company', async () => {
    const card = visibleCard(await mountSection())

    expect(card.text()).toContain('First quote')
    expect(card.text()).toContain('Ann Lee')
    expect(card.text()).toContain('CEO, Example Co')
  })

  it('shows the month and year the quote was given, in the active locale', async () => {
    const wrapper = await mountSection()

    const date = wrapper.find('[data-testid="testimonial-date"]')

    expect(date.text()).toBe('March 2026')
    expect(date.attributes('datetime')).toBe('2026-03-10T00:00:00+00:00')
  })

  it('draws the rating as filled and empty stars with an accessible label', async () => {
    const rating = (await mountSection()).find('[data-testid="testimonial-rating"]')

    expect(rating.attributes('aria-label')).toBe('aurora.testimonials.rating')
    expect(rating.findAll('svg')).toHaveLength(5)
    expect(rating.findAll('svg.text-amber-500')).toHaveLength(4)
  })

  it('shows initials on the brand gradient without an avatar, and the picture with one', async () => {
    const wrapper = await mountSection()
    const cards = wrapper.findAll('[data-testid="testimonial-card"]')

    expect(cards[0]!.find('[data-testid="testimonial-initials"]').text()).toBe('AL')
    expect(cards[0]!.find('img').exists()).toBe(false)
    expect(cards[1]!.find('img').attributes('src')).toBe('/uploads/bob.png')
    expect(cards[1]!.find('[data-testid="testimonial-initials"]').exists()).toBe(false)
  })

  it('draws no decorative quote glyph', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="testimonial-quote-mark"]').exists()).toBe(false)
  })

  it('sizes cards by their column width and never by a max-w cap', async () => {
    const card = visibleCard(await mountSection())

    expect(card.classes().some(name => name.startsWith('max-w-'))).toBe(false)
    expect(card.classes()).toContain('sm:w-[calc((100%-1.25rem)/2)]')
    expect(card.classes()).toContain('lg:w-[calc((100%-2.5rem)/3)]')
    expect(card.element.parentElement!.className).toContain('justify-center')
  })

  it('hides the arrows and dots where all quotes fit, and shows them when there are more than three', async () => {
    const two = await mountSection()
    expect(two.find('[data-testid="testimonials-arrows"]').classes()).toContain('sm:hidden')

    serve([1, 2, 3, 4].map(id => testimonialFixture(id, { quote: `Q${id}` })))
    const four = await mountSection()
    expect(four.find('[data-testid="testimonials-arrows"]').classes()).not.toContain('sm:hidden')
    expect(four.find('[data-testid="testimonials-arrows"]').classes()).not.toContain('lg:hidden')
  })

  it('has no controls for a single quote', async () => {
    serve([testimonialFixture(1, { quote: 'Only' })])

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="testimonials-arrows"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="testimonials-dots"]').exists()).toBe(false)
  })

  it('renders every quote in the window, wrapping past the last one', async () => {
    const wrapper = await mountSection()

    await wrapper.find('[data-testid="testimonials-next"]').trigger('click')

    const texts = wrapper.findAll('[data-testid="testimonial-card"]').map(card => card.text())
    expect(texts[0]).toContain('Second quote')
    expect(texts[1]).toContain('First quote')
  })

  it('moves with the left and right arrow keys', async () => {
    const wrapper = await mountSection()
    const track = wrapper.find('[data-testid="testimonials-track"]')

    await track.trigger('keydown', { key: 'ArrowRight' })
    expect(wrapper.find('[data-testid="testimonial-card"]').text()).toContain('Second quote')

    await track.trigger('keydown', { key: 'ArrowLeft' })
    expect(wrapper.find('[data-testid="testimonial-card"]').text()).toContain('First quote')
  })

  it('marks the dot of the leading quote as current', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="testimonials-dot-0"]').attributes('aria-current')).toBe('true')
    expect(wrapper.find('[data-testid="testimonials-dot-1"]').attributes('aria-current')).toBeUndefined()
  })

  it('advances with next and wraps back to the first quote', async () => {
    const wrapper = await mountSection()

    await wrapper.find('[data-testid="testimonials-next"]').trigger('click')
    expect(visibleCard(wrapper).text()).toContain('Second quote')

    await wrapper.find('[data-testid="testimonials-next"]').trigger('click')
    expect(visibleCard(wrapper).text()).toContain('First quote')
  })

  it('wraps from the first quote to the last with previous', async () => {
    const wrapper = await mountSection()

    await wrapper.find('[data-testid="testimonials-prev"]').trigger('click')

    expect(visibleCard(wrapper).text()).toContain('Second quote')
  })

  it('jumps to a quote from its dot', async () => {
    const wrapper = await mountSection()

    await wrapper.find('[data-testid="testimonials-dot-1"]').trigger('click')

    expect(visibleCard(wrapper).text()).toContain('Second quote')
  })

  it('renders nothing, arrows included, when there are no quotes', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="testimonial-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="testimonials-next"]').exists()).toBe(false)
  })
})
