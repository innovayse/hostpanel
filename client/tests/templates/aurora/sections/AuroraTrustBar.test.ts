/**
 * Unit tests for TrustBar: the three guarantees.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import TrustBar from '~/templates/aurora/sections/AuroraTrustBar.vue'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('TrustBar', () => {
  it('renders the three guarantees', async () => {
    await loadGlobals()
    const text = mountWithKit(TrustBar).text()

    expect(text).toContain('aurora.trust.ssl')
    expect(text).toContain('aurora.trust.refund')
    expect(text).toContain('aurora.trust.noHidden')
  })

  it('centres the guarantees in one card, each icon in a tinted chip, without a shadow', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(TrustBar)

    expect(wrapper.find('[data-testid="trust-bar"]').classes()).toEqual(expect.arrayContaining(['w-full', 'border-line']))
    expect(wrapper.find('[data-testid="trust-bar"]').classes()).not.toContain('shadow-panel')
    expect(wrapper.findAll('[data-testid="trust-item"]')).toHaveLength(3)
    expect(wrapper.findAll('[data-testid="trust-chip"]')).toHaveLength(3)
  })

  it('names no payment brand', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(TrustBar)

    expect(wrapper.find('[data-testid="trust-payment"]').exists()).toBe(false)
  })
})
