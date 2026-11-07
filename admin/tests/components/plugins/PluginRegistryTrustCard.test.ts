/**
 * Tests for PluginRegistryTrustCard: the stored level is shown, the strictest level is the default for an
 * empty row, the widest level carries a warning, a pick saves the literal enum string, a refusal shows the
 * API's sentence, and an unseeded row is replaced by a notice.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PluginRegistryTrustCard from '@/components/plugins/PluginRegistryTrustCard.vue'
import { PluginRegistryTrust } from '@/types/plugin'
import { PluginSettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'
import { pickOption } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockReset()
  save.mockResolvedValue({ saved: true, error: null })
})

/**
 * Builds the props of the card for one stored value.
 *
 * @param value - The stored value of the row, or null for a row the server never seeded.
 * @returns The props.
 */
const propsFor = (value: string | null) => ({
  settings: (value === null ? [] : [{ id: 11, key: PluginSettingKey.RegistryTrust, value }]) as Setting[],
  save,
})

describe('PluginRegistryTrustCard', () => {
  it('shows the stored level in the select', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(PluginRegistryTrust.OwnKeysOnly))

    expect(wrapper.get('[data-testid="plugin-registry-trust-select"]').text()).toContain('Packages signed with my own keys only')
    expect(wrapper.find('[data-testid="plugin-registry-trust-warning"]').exists()).toBe(false)
  })

  it('reads an empty row as verified publishers only', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(''))

    expect(wrapper.get('[data-testid="plugin-registry-trust-select"]').text()).toContain('Verified publishers only')
  })

  it('warns while any registry publisher is chosen', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(PluginRegistryTrust.AnyRegistryPublisher))

    expect(wrapper.get('[data-testid="plugin-registry-trust-warning"]').text()).toContain('nobody has verified')
  })

  it('saves the picked level as the literal string', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(PluginRegistryTrust.VerifiedOnly))

    await pickOption(wrapper, 'plugin-registry-trust-select', 'Any registry publisher')
    await wrapper.get('[data-testid="plugin-registry-trust-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[11, PluginRegistryTrust.AnyRegistryPublisher]])
  })

  it('does not save before anything changed', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(PluginRegistryTrust.VerifiedOnly))

    await wrapper.get('[data-testid="plugin-registry-trust-save"]').trigger('click')

    expect(save).not.toHaveBeenCalled()
  })

  it('shows the API sentence when the save is refused', async () => {
    save.mockResolvedValue({ saved: false, error: 'That value is not accepted for this setting.' })
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(PluginRegistryTrust.VerifiedOnly))

    await pickOption(wrapper, 'plugin-registry-trust-select', 'Any registry publisher')
    await wrapper.get('[data-testid="plugin-registry-trust-save"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('That value is not accepted for this setting.')
  })

  it('names the key and hides the select when the row was never seeded', async () => {
    const { wrapper } = await mountScreen(PluginRegistryTrustCard, propsFor(null))

    expect(wrapper.get('[data-testid="plugin-registry-trust-not-seeded"]').text()).toContain('plugins.registry_trust')
    expect(wrapper.find('[data-testid="plugin-registry-trust-select"]').exists()).toBe(false)
  })
})
