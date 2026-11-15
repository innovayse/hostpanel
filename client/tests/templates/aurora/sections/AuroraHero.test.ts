/**
 * Unit tests for AuroraHero: the single page headline, both calls to action and the operator-set badge and figures.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import AuroraHero from '~/templates/aurora/sections/AuroraHero.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

describe('AuroraHero', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubLocalePath()
    await loadPortalSettings(replies)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders the page headline as the only h1, with the accented phrase', () => {
    const wrapper = mountWithKit(AuroraHero)

    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.find('[data-testid="hero-title"]').text()).toContain('aurora.hero.titleAccent')
  })

  it('links the two calls to action to the domain search and the contact page', () => {
    const wrapper = mountWithKit(AuroraHero)

    expect(wrapper.find('[data-testid="hero-cta-primary"]').attributes('href')).toBe('/domains')
    expect(wrapper.find('[data-testid="hero-cta-secondary"]').attributes('href')).toBe('/contact')
  })

  it('shows the badge and the figures the operator configured', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.HeroBadge]: 'Fast and calm',
      [PortalSettingKey.StatsClients]: '1 200+',
      [PortalSettingKey.StatsUptime]: '99.9%',
      [PortalSettingKey.StatsRegions]: '3',
    })
    const wrapper = mountWithKit(AuroraHero)

    expect(wrapper.find('[data-testid="hero-badge"]').text()).toContain('Fast and calm')
    expect(wrapper.find('[data-testid="hero-stats"]').text()).toContain('1 200+')
    expect(wrapper.find('[data-testid="hero-stats"]').text()).toContain('99.9%')
    expect(wrapper.find('[data-testid="hero-stats"]').text()).toContain('aurora.hero.statRegions')
  })

  it('leaves out an unset figure and hides the badge and the figures row when nothing is set', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.StatsUptime]: '99.9%' })
    const partial = mountWithKit(AuroraHero)
    expect(partial.find('[data-testid="hero-stats"]').text()).toContain('99.9%')
    expect(partial.find('[data-testid="hero-stats"]').text()).not.toContain('aurora.hero.statClients')

    await loadPortalSettings(replies)
    const empty = mountWithKit(AuroraHero)
    expect(empty.find('[data-testid="hero-badge"]').exists()).toBe(false)
    expect(empty.find('[data-testid="hero-stats"]').exists()).toBe(false)
  })

  it('lays the figures out as one even stat group', async () => {
    const figure = 'Europe, North America'
    await loadPortalSettings(replies, {
      [PortalSettingKey.StatsClients]: figure,
      [PortalSettingKey.StatsUptime]: figure,
      [PortalSettingKey.StatsRegions]: figure,
    })
    const wrapper = mountWithKit(AuroraHero)

    expect(wrapper.find('[data-testid="hero-stats"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-stat-group-item"]')).toHaveLength(3)
  })

  it('paints the primary button with the brand gradient and the brand tint text', () => {
    const classes = mountWithKit(AuroraHero).find('[data-testid="hero-cta-primary"]').classes()

    expect(classes).toEqual(expect.arrayContaining(['bg-brand', 'text-on-brand']))
  })
})
