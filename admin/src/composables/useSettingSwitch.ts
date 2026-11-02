/**
 * The boolean view of a settings row that is stored as the string `true` or `false`.
 *
 * Every on/off settings control (the generic toggle row, the plugin signing switch) shows the same
 * rule: only the literal `true` is on, and a flip writes the literal string back into the row's draft.
 * It lives here so the mapping is written once.
 */
import { computed } from 'vue'
import type { WritableComputedRef } from 'vue'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { SettingToggle } from '@/types/setting'

/**
 * Wraps a draft of an on/off row as a boolean for a switch.
 *
 * @param draft - The draft of the row, from `useSettingDraft`.
 * @returns A writable boolean: true only for the literal `true`; writing stores `true` or `false` in the draft.
 */
export const useSettingSwitch = (draft: SettingDraft): WritableComputedRef<boolean> =>
  computed<boolean>({
    /**
     * Whether the switch shows as on.
     *
     * @returns True only for the literal `true`.
     */
    get: () => draft.value.value === SettingToggle.On,
    /**
     * Writes the switch back as the string the API stores.
     *
     * @param on - Whether the switch is now on.
     */
    set: (on: boolean) => {
      draft.value.value = on ? SettingToggle.On : SettingToggle.Off
    },
  })
