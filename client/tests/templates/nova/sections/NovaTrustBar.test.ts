/**
 * Unit tests for TrustBar: qualitative lines plus the operator's own uptime figure.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import TrustBar from '~/templates/nova/sections/NovaTrustBar.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { TRUST_ITEMS } from '~/templates/nova/content'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('TrustBar', () => {
  it('prints no uptime figure until the operator publishes one', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(TrustBar)

    expect(wrapper.text()).not.toContain('nova.trust.uptimeLabel')
  })

  it('prints the operator figure verbatim when set', async () => {
    await loadGlobals({ settings: { [PortalSettingKey.StatsUptime]: '99.9%' } })
    const wrapper = mountWithKit(TrustBar)

    expect(wrapper.find('strong').text()).toBe('99.9%')
    expect(wrapper.text()).toContain('nova.trust.uptimeLabel')
  })

  it('hides gated lines until their setting is filled in', async () => {
    await loadGlobals()
    const gated = TRUST_ITEMS.filter(item => item.requiresSetting).length

    expect(mountWithKit(TrustBar).findAll('li')).toHaveLength(TRUST_ITEMS.length - gated)
  })
})
