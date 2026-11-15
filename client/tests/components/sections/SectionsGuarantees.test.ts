/**
 * Unit tests for SectionsGuarantees: one card per guarantee that is set and no section when none is.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import SectionsGuarantees from '~/components/sections/SectionsGuarantees.vue'
import { useGuarantees } from '~/composables/useGuarantees'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

/** Free migration on and a 30 day money-back period; the no-hidden-fees promise is left unset. */
const SET_GUARANTEES: Record<string, string> = {
  [PortalSettingKey.MigrationFree]: SETTING_ON,
  [PortalSettingKey.GuaranteeMoneyBackDays]: '30',
}

describe('SectionsGuarantees', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    vi.stubGlobal('useGuarantees', useGuarantees)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('shows a card for each guarantee that is set', async () => {
    await loadPortalSettings(replies, SET_GUARANTEES)

    const wrapper = mountWithKit(SectionsGuarantees, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="guarantee-free_migration"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="guarantee-money_back"]').text()).toContain('hostingSections.guarantees.items.money_back.title')
  })

  it('leaves out a guarantee that is not set', async () => {
    await loadPortalSettings(replies, SET_GUARANTEES)

    const wrapper = mountWithKit(SectionsGuarantees, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="guarantee-no_hidden_fees"]').exists()).toBe(false)
  })

  it('renders nothing when no guarantee is set', async () => {
    await loadPortalSettings(replies)

    const wrapper = mountWithKit(SectionsGuarantees, { components: CLASSIC_COMPONENTS })

    expect(wrapper.find('[data-testid="sections-guarantees"]').exists()).toBe(false)
  })

  it('draws every icon on a flat brand tile with no accent glow', async () => {
    await loadPortalSettings(replies, SET_GUARANTEES)

    const wrapper = mountWithKit(SectionsGuarantees, { components: CLASSIC_COMPONENTS })

    const tiles = wrapper.findAll('[data-testid="classic-icon-tile"]')
    expect(tiles.length).toBeGreaterThan(0)
    expect(wrapper.html()).not.toContain('--accent')
  })
})
