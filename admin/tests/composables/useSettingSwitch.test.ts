/**
 * Tests for useSettingSwitch: only the literal `true` reads as on, and writing the switch stores
 * the literal string in the draft.
 */
import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import { useSettingDraft } from '@/composables/useSettingDraft'
import { useSettingSwitch } from '@/composables/useSettingSwitch'
import type { Setting } from '@/types/setting'

/**
 * Builds the switch of a row holding one stored value.
 *
 * @param value - Stored value of the row under test.
 * @param fallback - What an empty row reads as.
 * @returns The draft and its switch.
 */
const switchFor = (value: string, fallback = 'false') => {
  /** The settings list holding the one row. */
  const settings = ref<Setting[]>([{ id: 3, key: 'plugins.allow_unsigned', value, isSecret: false, hasValue: value.length > 0 }])
  /** The draft of the row, read by the switch. */
  const draft = useSettingDraft(settings, 'plugins.allow_unsigned', fallback)
  return { draft, enabled: useSettingSwitch(draft) }
}

describe('useSettingSwitch', () => {
  it('reads only the literal true as on', () => {
    expect(switchFor('true').enabled.value).toBe(true)
    expect(switchFor('false').enabled.value).toBe(false)
    expect(switchFor('yes').enabled.value).toBe(false)
  })

  it('reads an empty row as its fallback', () => {
    expect(switchFor('', 'true').enabled.value).toBe(true)
    expect(switchFor('', 'false').enabled.value).toBe(false)
  })

  it('writes the literal string back into the draft and marks it dirty', () => {
    const { draft, enabled } = switchFor('false')

    enabled.value = true

    expect(draft.value.value).toBe('true')
    expect(draft.dirty.value).toBe(true)
    enabled.value = false
    expect(draft.value.value).toBe('false')
    expect(draft.dirty.value).toBe(false)
  })
})
