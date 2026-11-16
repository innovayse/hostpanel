/**
 * Unit tests for AuroraServices: four service cards, each with a named gradient tile.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AuroraServices from '~/templates/aurora/sections/AuroraServices.vue'
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { HomeSectionId } from '~/types/homesectionid'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('AuroraServices', () => {
  it('carries the anchor id the header links to, with room above it', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraServices)

    expect(wrapper.attributes('id')).toBe(HomeSectionId.Services)
    expect(wrapper.classes()).toContain(HOME_SECTION_SCROLL_MARGIN_CLASS)
  })

  it('keeps the full catalogue one click away', async () => {
    await loadGlobals()
    const link = mountWithKit(AuroraServices).find('[data-testid="services-view-all"]')

    expect(link.attributes('href')).toBe('/products')
    expect(link.text()).toBe('aurora.services.viewAll')
  })

  it('renders the four services with their copy', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraServices)

    for (const tag of ['hosting', 'servers', 'domains', 'billing']) {
      expect(wrapper.find(`[data-testid="service-card-${tag}"]`).exists()).toBe(true)
    }
    expect(wrapper.text()).toContain('aurora.services.domainsBody')
  })

  it('puts an icon, not a text tag, in every tile', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraServices)

    for (const key of ['hosting', 'servers', 'domains', 'billing']) {
      const tile = wrapper.find(`[data-testid="service-icon-${key}"]`)
      expect(tile.find('svg').exists()).toBe(true)
      expect(tile.text()).toBe('')
      expect(tile.classes()).toContain('text-white')
      expect(tile.classes()).toContain('h-10')
    }
    expect(wrapper.text()).not.toContain('Tag')
  })

  it('paints each tile from a gradient token, never an inline style', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AuroraServices)
    const tile = wrapper.find('[data-testid="service-card-hosting"]').find('.place-items-center')

    expect(tile.classes()).toContain('bg-tile-sky')
    expect(wrapper.html()).not.toContain(' style=')
  })

  it('gives every card the shared resting depth and a lift on hover', () => {
    const wrapper = mountWithKit(AuroraServices)
    const card = wrapper.find('[data-testid="service-card-hosting"]')

    expect(card.classes()).toEqual(expect.arrayContaining(['shadow-card', 'hover:-translate-y-0.5']))
  })
})
