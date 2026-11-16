/**
 * Unit tests for SectionsHero: the hosting headline, the domain search as the main action, the
 * secondary plans action, the operator badge and the operator-set figures (hidden while empty).
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import SectionsHero from '~/components/sections/SectionsHero.vue'
import SectionsDomainSearch from '~/components/sections/SectionsDomainSearch.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** All three figures set. */
const ALL_FIGURES: Record<string, string> = {
  [PortalSettingKey.StatsClients]: '150+',
  [PortalSettingKey.StatsUptime]: '99.9%',
  [PortalSettingKey.StatsRegions]: '3',
}

/** Mounts the hero with the domain search registered as the auto-import Nuxt would provide. */
const mountHero = () => mountWithKit(SectionsHero, { components: { SectionsDomainSearch } })

describe('SectionsHero', () => {
  beforeEach(async () => {
    stubPublicPageRuntime()
    await loadPortalSettings(replies, ALL_FIGURES)
  })

  it('shows the headline as the page heading, with the badge and subtitle', () => {
    const wrapper = mountHero()

    expect(wrapper.find('h1').text()).toContain('hero.title')
    expect(wrapper.find('[data-testid="hero-badge"]').text()).toBe('hero.badge')
    expect(wrapper.text()).toContain('hero.subtitle')
  })

  it('prefers the operator badge line over the translated default', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.HeroBadge]: 'Hosting since 2010' })

    expect(mountHero().find('[data-testid="hero-badge"]').text()).toBe('Hosting since 2010')
  })

  it('offers the domain search as the main action and the hosting plans as the secondary one', () => {
    const wrapper = mountHero()

    expect(wrapper.find('[data-testid="sections-domain-search"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="hero-secondary"]').attributes('href')).toBe('/en/hosting')
  })

  it('shows each configured figure with its caption', () => {
    const group = mountHero().find('[data-testid="hero-stats"]')

    expect(group.text()).toContain('150+')
    expect(group.text()).toContain('hero.stats.clients')
    expect(group.text()).toContain('99.9%')
    expect(group.text()).toContain('hero.stats.uptime')
    expect(group.text()).toContain('hero.stats.regions')
  })

  it('leaves out a figure whose setting is empty and hides the block when none is set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.StatsUptime]: '99.9%' })
    const partial = mountHero()

    expect(partial.findAll('[data-testid="ui-stat-group-item"]')).toHaveLength(1)
    expect(partial.text()).not.toContain('hero.stats.clients')

    await loadPortalSettings(replies, {})

    expect(mountHero().find('[data-testid="hero-stats"]').exists()).toBe(false)
  })

  it('draws no gradient or glow decoration', () => {
    const html = mountHero().html()

    expect(html).not.toContain('bg-gradient')
    expect(html).not.toContain('blur-')
    expect(html).not.toContain('animate-blob')
  })
})
