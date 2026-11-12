/**
 * Unit tests for ProductsFullSlider: a slide per catalogue slide in its own brand colour, the
 * calls to action, the arrows that only show with several slides, and the scroll-down button. The
 * carousel library is stood in for.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { mountPage, registerPageComponents, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsProductsSlide from '~/components/sections/SectionsProductsSlide.vue'
import ProductsFullSlider from '~/components/sections/SectionsProductsFullSlider.vue'
import type { SlidePublic } from '~/types/slidepublic'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The slide jumps the stand-in carousel was asked for. */
const slideToLoop = vi.hoisted(() => vi.fn())

vi.mock('swiper/vue', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    /** Stand-in carousel: renders its slides in a plain element and reports a fake instance on the second slide. */
    Swiper: defineComponent({
      name: 'Swiper',
      emits: ['swiper'],
      setup: (_props, { slots, emit }) => {
        emit('swiper', { realIndex: 1, slideToLoop })
        return () => h('div', { 'data-testid': 'swiper' }, slots.default?.())
      },
    }),
    /** Stand-in slide. */
    SwiperSlide: defineComponent({ name: 'SwiperSlide', setup: (_props, { slots }) => () => h('div', slots.default?.()) }),
  }
})
vi.mock('swiper/modules', () => ({ Autoplay: {}, Navigation: {} }))
vi.mock('swiper/css', () => ({}))
vi.mock('swiper/css/navigation', () => ({}))

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the slides it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these slides.
 *
 * @param slides The slides the public read answers with.
 */
const serve = (slides: SlidePublic[]): void => {
  replies[`GET ${CatalogPath.Slides}`] = { body: slides }
}

/**
 * Builds a slide.
 *
 * @param id The slide id.
 * @param overrides Fields that differ from the defaults.
 * @returns The slide.
 */
const slide = (id: number, overrides: Partial<SlidePublic> = {}): SlidePublic => ({
  id,
  iconName: 'school',
  brandColor: '#8b5cf6',
  imageUrl: `/images/slide-${id}.jpg`,
  productId: null,
  content: { title: `Slide ${id}`, tagline: 'Tagline', description: 'Description', features: ['Feature one'] },
  links: { demoUrl: null, learnMoreUrl: `/products/slide-${id}` },
  callToAction: { text: null, url: null },
  pricing: { monthlyPrice: null, annualPrice: null, fromPrice: null },
  ...overrides,
})

/**
 * Mounts the slider and lets the slides load.
 *
 * @returns The wrapper.
 */
const mountSlider = async () => {
  // The hero is auto-imported by Nuxt, so the test registers a stand-in that keeps its test id.
  const wrapper = await mountPage(ProductsFullSlider, { stubs: { SectionsHero: { template: '<div data-testid="sections-hero" />' } } })
  // The slides cross a real socket, and nothing renders until they arrive: the slider, or the hero in its place.
  await waitUntil(() => expect(wrapper.find('[data-testid="products-slider"], [data-testid="sections-hero"]').exists()).toBe(true))
  await flushPromises()
  return wrapper
}

describe('ProductsFullSlider', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubPublicPageRuntime()
    slideToLoop.mockReset()
    registerPageComponents({ SectionsProductsSlide })
    serve([slide(1), slide(2, { links: { demoUrl: 'https://demo.test', learnMoreUrl: '/products/slide-2' }, brandColor: '#10b981' })])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('reads the slides in the active language', async () => {
    await mountSlider()

    const reads = requestsTo(stub.requests, CatalogPath.Slides)
    expect(reads).toHaveLength(1)
    expect(reads[0]?.method).toBe('GET')
    expect(queryOf(reads[0]!)).toEqual({ lang: 'en' })
  })

  it('renders a slide per catalogue slide with its title, tagline and features', async () => {
    const wrapper = await mountSlider()

    const text = wrapper.text()
    expect(text).toContain('Slide 1')
    expect(text).toContain('Slide 2')
    expect(text).toContain('Tagline')
    expect(text).toContain('Feature one')
  })

  it('paints each slide in its brand colour', async () => {
    const wrapper = await mountSlider()

    const colours = wrapper.findAll('[data-testid="ui-accent"]').map((node) => (node.element as HTMLElement).style.getPropertyValue('--accent'))
    expect(colours).toEqual(expect.arrayContaining(['#8b5cf6', '#10b981']))
  })

  it('links learn more to the slide page, and offers a demo only where the slide has one', async () => {
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="slider-cta-1"]').attributes('href')).toBe('/en/products/slide-1')
    expect(wrapper.find('[data-testid="slider-demo-1"]').exists()).toBe(false)
    const demo = wrapper.find('[data-testid="slider-demo-2"]')
    expect(demo.attributes('href')).toBe('https://demo.test')
    expect(demo.attributes('rel')).toBe('noopener noreferrer')
  })

  it('shows the arrows only when there is more than one slide', async () => {
    const several = await mountSlider()
    expect((several.find('[data-testid="slider-prev"]').element as HTMLElement).style.display).not.toBe('none')

    serve([slide(1)])
    const single = await mountSlider()
    expect((single.find('[data-testid="slider-prev"]').element as HTMLElement).style.display).toBe('none')
  })

  it('falls back to the text hero when the operator has no slides, instead of an empty band', async () => {
    serve([])
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="products-slider"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sections-hero"]').exists()).toBe(true)
  })

  it('follows the colour mode itself: the panel is never forced into the dark palette', async () => {
    const wrapper = await mountSlider()

    const panel = wrapper.find('[data-testid="products-slider"]')
    expect(panel.classes()).not.toContain('dark')
    expect(panel.classes()).toContain('bg-classic-page')
  })

  it('lays each slide out as a split: text on the left, the cover on the right, the cover first on a phone', async () => {
    const wrapper = await mountSlider()

    const grid = wrapper.find('[data-testid="slider-slide-1"]')
    expect(grid.classes()).toEqual(expect.arrayContaining(['grid', 'grid-cols-1', 'md:grid-cols-2', 'h-full']))
    const cover = wrapper.find('[data-testid="slider-cover-1"]')
    const text = wrapper.find('[data-testid="slider-text-1"]')
    expect(cover.classes()).toContain('md:order-2')
    expect(text.classes()).toContain('md:order-1')
    // The cover comes first in the markup, so a phone shows it above the text.
    expect(grid.element.firstElementChild).toBe(cover.element)
  })

  it('shows the uploaded picture as the cover, and keeps the panel in the page colour with no scrim', async () => {
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="slider-cover-image-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="slider-cover-art-1"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('bg-black')
    expect(wrapper.find('[data-testid="products-slider"]').classes()).not.toContain('dark')
  })

  it('falls back to the illustration of the slide topic when it has no picture, and to the generic one otherwise', async () => {
    serve([
      slide(1, { imageUrl: '', links: { demoUrl: null, learnMoreUrl: '/vps' } }),
      slide(2, { imageUrl: '', links: { demoUrl: null, learnMoreUrl: '/offers' } }),
    ])
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="slider-cover-art-1"]').classes().join(' ')).toContain('/covers/vps.svg')
    expect(wrapper.find('[data-testid="slider-cover-art-2"]').classes().join(' ')).toContain('/covers/generic.svg')
  })

  it('has no scroll-down button', async () => {
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="slider-scroll"]').exists()).toBe(false)
  })

  it('drives the kit dots from the carousel: the active dot follows it, and a dot press moves it', async () => {
    const wrapper = await mountSlider()

    // The stand-in carousel reports the second slide as active.
    expect(wrapper.find('[data-testid="carousel-dot-1"]').attributes('aria-current')).toBe('true')
    expect(wrapper.find('[data-testid="carousel-dot-0"]').attributes('aria-current')).toBeUndefined()

    await wrapper.find('[data-testid="carousel-dot-0"]').trigger('click')

    expect(slideToLoop).toHaveBeenCalledWith(0)
  })

  it('shows no dots for a single slide', async () => {
    serve([slide(1)])
    const wrapper = await mountSlider()

    expect(wrapper.find('[data-testid="carousel-dots"]').exists()).toBe(false)
  })

  it('renders no image for a slide whose image is empty', async () => {
    serve([slide(1, { imageUrl: '' })])
    const wrapper = await mountSlider()

    expect(wrapper.text()).toContain('Slide 1')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('nuxt-img-stub').exists()).toBe(false)
  })
})
