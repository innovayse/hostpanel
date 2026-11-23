/**
 * Unit tests for SectionsProductsSlide: the split layout (cover first in the markup, text beside
 * it), the cover choice between the uploaded picture and the topic illustration, the eager
 * loading of the first slide's picture, and the calls to action.
 *
 * @module tests/components/sections/SectionsProductsSlide.test
 */

import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, TEST_LOCALE_PREFIX } from '~/tests/helpers/runtimeStubs'
import SectionsProductsSlide from '~/components/sections/SectionsProductsSlide.vue'
import type { SliderItem } from '~/types/slideritem'

/**
 * Builds a slide.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The slide.
 */
const item = (overrides: Partial<SliderItem> = {}): SliderItem => ({
  id: '7',
  name: 'Cloud VPS',
  tagline: 'Full control',
  description: 'Isolated virtual servers.',
  features: ['Root access', 'NVMe storage'],
  icon: 'server',
  color: '#6366f1',
  image: '/demo/slides/cloud-vps.svg',
  demoUrl: '',
  learnMoreUrl: '/vps',
  ...overrides,
})

/**
 * Mounts the slide.
 *
 * @param slide The slide to show.
 * @param eager Whether the cover loads at once.
 * @returns The wrapper.
 */
const mountSlide = (slide: SliderItem, eager = false) =>
  mountWithKit(SectionsProductsSlide, { props: { item: slide, eager } })

describe('SectionsProductsSlide', () => {
  beforeEach(() => {
    stubLocalePath(TEST_LOCALE_PREFIX)
  })

  it('shows the title, tagline, description and a chip per feature', () => {
    const wrapper = mountSlide(item())

    const text = wrapper.text()
    expect(text).toContain('Cloud VPS')
    expect(text).toContain('Full control')
    expect(text).toContain('Isolated virtual servers.')
    expect(wrapper.find('[data-testid="slider-features-7"]').text()).toContain('Root access')
    expect(wrapper.find('[data-testid="slider-features-7"]').text()).toContain('NVMe storage')
  })

  it('leaves out the tagline, the description and the feature row when the slide has none', () => {
    const wrapper = mountSlide(item({ tagline: '', description: '', features: [] }))

    expect(wrapper.text()).not.toContain('Full control')
    expect(wrapper.text()).not.toContain('Isolated virtual servers.')
    expect(wrapper.find('[data-testid="slider-features-7"]').exists()).toBe(false)
  })

  it('puts the cover first in the markup, so a phone shows it above the text', () => {
    const wrapper = mountSlide(item())

    const grid = wrapper.find('[data-testid="slider-slide-7"]')
    expect(grid.element.firstElementChild).toBe(wrapper.find('[data-testid="slider-cover-7"]').element)
    expect(wrapper.find('[data-testid="slider-cover-7"]').classes()).toContain('md:order-2')
    expect(wrapper.find('[data-testid="slider-text-7"]').classes()).toContain('md:order-1')
  })

  it('uses the uploaded picture as the cover, described by the slide name', () => {
    const wrapper = mountSlide(item())

    const image = wrapper.find('[data-testid="slider-cover-image-7"]')
    expect(image.exists()).toBe(true)
    expect(image.attributes('alt')).toBe('Cloud VPS')
    expect(wrapper.find('[data-testid="slider-cover-art-7"]').exists()).toBe(false)
  })

  it('loads the picture lazily, and at once for the first slide', () => {
    const lazy = mountSlide(item())
    const eager = mountSlide(item(), true)

    expect(lazy.find('[data-testid="slider-cover-image-7"]').attributes('loading')).toBe('lazy')
    expect(eager.find('[data-testid="slider-cover-image-7"]').attributes('loading')).toBe('eager')
  })

  it('draws the illustration of the slide topic when there is no picture', () => {
    const wrapper = mountSlide(item({ image: '', learnMoreUrl: '/domains' }))

    expect(wrapper.find('[data-testid="slider-cover-image-7"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="slider-cover-art-7"]').classes().join(' ')).toContain('/covers/domain.svg')
  })

  it('falls back to the generic illustration for a topic it does not know', () => {
    const wrapper = mountSlide(item({ image: '', learnMoreUrl: '/offers' }))

    expect(wrapper.find('[data-testid="slider-cover-art-7"]').classes().join(' ')).toContain('/covers/generic.svg')
  })

  it('links learn more under the active language, and offers the demo only when the slide has one', () => {
    const without = mountSlide(item())
    const withDemo = mountSlide(item({ demoUrl: 'https://demo.test' }))

    expect(without.find('[data-testid="slider-cta-7"]').attributes('href')).toBe('/en/vps')
    expect(without.find('[data-testid="slider-demo-7"]').exists()).toBe(false)
    const demo = withDemo.find('[data-testid="slider-demo-7"]')
    expect(demo.attributes('href')).toBe('https://demo.test')
    expect(demo.attributes('rel')).toBe('noopener noreferrer')
    expect(without.find('[data-testid="slider-cta-7"]').text()).toBe('common.learnMore')
    expect(demo.text()).toBe('products.pricing.cta.viewDemo')
  })

  it('offers no demo button for an address with an unsafe scheme', () => {
    const wrapper = mountSlide(item({ demoUrl: 'javascript:alert(1)' }))

    expect(wrapper.find('[data-testid="slider-demo-7"]').exists()).toBe(false)
  })

  it('paints the slide in its own colour', () => {
    const wrapper = mountSlide(item())

    expect((wrapper.find('[data-testid="ui-accent"]').element as HTMLElement).style.getPropertyValue('--accent')).toBe('#6366f1')
  })
})
