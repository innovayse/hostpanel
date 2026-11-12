/**
 * Unit tests for AuroraPartners: the partners come from the storefront store, show a logo or the name, link only with a website, and the section hides when there are none.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { partnerFixture } from '~/tests/helpers/storefrontFixtures'
import { mountAfterReads, StorefrontPath } from '~/tests/helpers/storefrontSection'
import { routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { Partner } from '~/types/partner'
import AuroraPartners from '~/templates/aurora/sections/AuroraPartners.vue'
import { stubAuroraGlobals } from '../auroraGlobals'
import { balancedColumns } from '~/utils/balancedGrid'

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route; the tests below change the partners it serves. */
const replies: StubRouteTable = {}

/**
 * Makes the stub serve these partners.
 *
 * @param partners The partners the public read answers with.
 */
const serve = (partners: Partner[]): void => {
  replies[readOf(StorefrontPath.Partners)] = { body: partners }
}

/**
 * Mounts the section and lets the content load.
 *
 * @returns The wrapper.
 */
const mountSection = async () => {
  stubAuroraGlobals()
  return mountAfterReads(stub, [StorefrontPath.Partners], () => mountWithKit(AuroraPartners))
}

describe('AuroraPartners', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    serve([
      partnerFixture(1, { name: 'Alpha', logoUrl: 'https://cdn.example.test/a.png', url: 'https://alpha.example.test' }),
      partnerFixture(2, { name: 'Beta', logoUrl: null, url: null }),
    ])
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders the logo when a partner has one and the name otherwise', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="partner-logo-1"]').attributes('src')).toBe('https://cdn.example.test/a.png')
    expect(wrapper.find('[data-testid="partner-logo-2"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="partner-logo-1"]').attributes('alt')).toBe('Alpha')
    expect(wrapper.find('[data-testid="partner-2"]').text()).toContain('Beta')
  })

  it('links only the partners that have a website', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="partner-1"]').attributes('href')).toBe('https://alpha.example.test')
    expect(wrapper.find('[data-testid="partner-2"]').attributes('href')).toBeUndefined()
  })

  it('renders nothing while there are no partners', async () => {
    serve([])

    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="aurora-partners"]').exists()).toBe(false)
    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('lays the cards out in the shared balanced grid, so no card is left alone on a row', async () => {
    serve(Array.from({ length: 5 }, (_, index) => partnerFixture(index + 1)))

    const wrapper = await mountSection()

    const grid = wrapper.find('[data-testid="aurora-partners-grid"]')
    expect(grid.exists()).toBe(true)
    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(5)))
    expect(grid.classes()).toContain('ui-balanced-grid')
  })

  it('runs the logos as a looping marquee once the list is long', async () => {
    serve(Array.from({ length: 8 }, (_, index) => partnerFixture(index + 1)))

    const wrapper = await mountSection()

    expect(wrapper.find('.animate-marquee').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="partner-1"]')).toHaveLength(2)
  })

  it('mutes the logos so they work on both themes and gain colour on hover', async () => {
    const wrapper = await mountSection()

    expect(wrapper.find('[data-testid="partner-logo-1"]').classes()).toEqual(expect.arrayContaining(['grayscale', 'group-hover:grayscale-0']))
  })
})
