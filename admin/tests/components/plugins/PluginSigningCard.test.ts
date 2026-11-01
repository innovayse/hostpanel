/**
 * Tests for PluginSigningCard: the warning is always visible, the switch shows the stored value, a flip
 * saves the literal string, a refusal shows the API's sentence, and an unseeded row is replaced by a notice.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PluginSigningCard from '@/components/plugins/PluginSigningCard.vue'
import { PluginSettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'

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
  settings: (value === null
    ? []
    : [{ id: 9, key: PluginSettingKey.AllowUnsigned, value, isSecret: false, hasValue: true }]) as Setting[],
  save,
})

describe('PluginSigningCard', () => {
  it('always shows the warning, with the switch off by default', async () => {
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor('false'))

    expect(wrapper.get('[data-testid="plugin-signing-warning"]').text()).toContain('run code on the server')
    expect(wrapper.get('[data-testid="plugin-allow-unsigned"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.find('[data-testid="plugin-signing-active"]').exists()).toBe(false)
  })

  it('shows the switch on and a louder notice while unsigned plugins are accepted', async () => {
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor('true'))

    expect(wrapper.get('[data-testid="plugin-allow-unsigned"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="plugin-signing-active"]').text()).toContain('Unsigned plugins are being accepted')
    expect(wrapper.find('[data-testid="plugin-signing-warning"]').exists()).toBe(true)
  })

  it('saves the flipped value as the literal string', async () => {
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor('false'))

    await wrapper.get('[data-testid="plugin-allow-unsigned"]').trigger('click')
    await wrapper.get('[data-testid="plugin-allow-unsigned-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[9, 'true']])
  })

  it('does not save before anything changed', async () => {
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor('false'))

    await wrapper.get('[data-testid="plugin-allow-unsigned-save"]').trigger('click')

    expect(save).not.toHaveBeenCalled()
  })

  it('shows the API sentence when the save is refused', async () => {
    save.mockResolvedValue({ saved: false, error: 'That value is not accepted for this setting.' })
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor('false'))

    await wrapper.get('[data-testid="plugin-allow-unsigned"]').trigger('click')
    await wrapper.get('[data-testid="plugin-allow-unsigned-save"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('That value is not accepted for this setting.')
  })

  it('names the key and hides the switch when the row was never seeded', async () => {
    const { wrapper } = await mountScreen(PluginSigningCard, propsFor(null))

    expect(wrapper.get('[data-testid="plugin-signing-not-seeded"]').text()).toContain('plugins.allow_unsigned')
    expect(wrapper.find('[data-testid="plugin-allow-unsigned"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-signing-warning"]').exists()).toBe(true)
  })
})
