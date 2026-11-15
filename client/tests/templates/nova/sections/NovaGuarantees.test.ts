/**
 * Unit tests for NovaGuarantees: a card per guarantee the operator has set, hidden when none.
 *
 * @module tests/templates/nova/sections/NovaGuarantees.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings } from '~/tests/helpers/portalSettings'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import NovaGuarantees from '~/templates/nova/sections/NovaGuarantees.vue'
import { useGuarantees } from '~/composables/useGuarantees'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'

/** The stub playing the BFF; it answers the operator's settings read. */
let stub: StubServer

/** What the stub answers, by route. */
const replies: StubRouteTable = {}

describe('NovaGuarantees', () => {
  beforeEach(async () => {
    stub = await startTransport(routeTable(replies))
    stubLocalePath()
    vi.stubGlobal('useGuarantees', useGuarantees)
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('renders one card per guarantee', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.MigrationFree]: SETTING_ON, [PortalSettingKey.GuaranteeMoneyBackDays]: '30' })
    const wrapper = mountWithKit(NovaGuarantees)

    expect(wrapper.findAll('[data-testid="nova-guarantees-grid"] > li')).toHaveLength(2)
    expect(wrapper.find('[data-testid="nova-guarantee-money_back"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="nova-guarantee-no_hidden_fees"]').exists()).toBe(false)
  })

  it('renders nothing when no guarantee is set', async () => {
    await loadPortalSettings(replies)

    expect(mountWithKit(NovaGuarantees).find('[data-testid="nova-guarantees"]').exists()).toBe(false)
  })
})
