/**
 * Tests for SettingsTextField: the free-text field of a settings card, filling its column, flagging a refused value and
 * showing the outcome of its save, with the warning for a row the API never seeded.
 */
import { defineComponent, h, toRef } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import SettingsTextField from '@/components/settings/SettingsTextField.vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'

/** The key the host edits. */
const KEY = 'portal.hero.badge'

/**
 * A card with one field, the way a real card builds it: the draft from the settings it is handed.
 *
 * @param extra - Further props of the field.
 * @returns The card component.
 */
const hostOf = (extra: Record<string, unknown> = {}) =>
  defineComponent({
    props: { settings: { type: Array as () => Setting[], required: true } },
    setup: (props) => {
      const draft = useSettingDraft(toRef(props, 'settings'), KEY)
      return () => h(SettingsTextField, {
        draft,
        settingKey: KEY,
        envName: 'ENV_BADGE',
        label: 'Hero badge',
        placeholder: 'Short line',
        maxlength: 40,
        testid: 'badge',
        ...extra,
      })
    },
  })

/** The row the field edits. */
const rows: Setting[] = [{ id: 7, key: KEY, value: 'New' }]

describe('SettingsTextField', () => {
  it('shows the stored value under its caption, in an input that fills its column', async () => {
    const { wrapper } = await mountSettingsCard(hostOf(), rows)
    const input = wrapper.get('[data-testid="badge"]')

    expect((input.element as HTMLInputElement).value).toBe('New')
    expect(input.attributes('placeholder')).toBe('Short line')
    expect(input.attributes('maxlength')).toBe('40')
    expect(wrapper.text()).toContain('Hero badge')
    expect(input.classes().join(' ')).not.toMatch(/min-w-|\bflex-1\b/)
  })

  it('records an edit as a draft the page bar saves', async () => {
    const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))
    const { wrapper, registry } = await mountSettingsCard(hostOf(), rows)

    await wrapper.get('[data-testid="badge"]').setValue('Sale')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[7, 'Sale']])
  })

  it('flags a refused value under the field, and shows a help line only while there is no refusal', async () => {
    const hinted = await mountSettingsCard(hostOf({ hint: 'Shown in the hero' }), rows)
    expect(hinted.wrapper.text()).toContain('Shown in the hero')
    expect(hinted.wrapper.find('[data-testid="badge-invalid"]').exists()).toBe(false)

    const refused = await mountSettingsCard(hostOf({ hint: 'Shown in the hero', invalidMessage: 'Too long.' }), rows)
    expect(refused.wrapper.get('[data-testid="badge-invalid"]').text()).toBe('Too long.')
    expect(refused.wrapper.get('[data-testid="badge"]').attributes('aria-invalid')).toBe('true')
    expect(refused.wrapper.text()).not.toContain('Shown in the hero')
  })

  it('says which row is missing instead of drawing a dead field', async () => {
    const { wrapper } = await mountSettingsCard(hostOf(), [])

    expect(wrapper.find('[data-testid="badge"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-not-seeded"]').text()).toContain(KEY)
  })
})
