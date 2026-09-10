import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import type { Setting } from '../../../types/setting'

/**
 * Draft-then-save state for one settings row, by key.
 *
 * The rule every settings control on the System Settings page follows: show the
 * operator's unsaved edit when there is one, the stored value otherwise, and clear
 * the edit only when the stored value catches up — never on the save request.
 * Clearing on emit discarded the operator's pick before anyone knew whether it had
 * been written, so a failed save silently snapped the control back to the value
 * already in use with only an error banner to explain it.
 *
 * @param settings - All system settings, as loaded from the backend.
 * @param key - The settings key this control edits.
 * @param fallback - Shown when the row exists but is empty, or does not exist.
 * @returns The row, the editable value, whether it changed, and a saver.
 */
export function useSettingDraft(settings: Ref<Setting[]>, key: string, fallback = '') {
  /** The stored row, or undefined when the key was never seeded. */
  const setting = computed(() => settings.value.find(s => s.key === key))

  /** The operator's unsaved edit; null when there is none. */
  const draft = ref<string | null>(null)

  /** What the control shows: the edit, else the stored value, else the fallback. */
  const value = computed({
    get: () => draft.value ?? setting.value?.value ?? fallback,
    set: (next: string) => { draft.value = next },
  })

  /** Whether there is an edit worth saving. */
  const dirty = computed(() => draft.value !== null && draft.value !== (setting.value?.value ?? ''))

  watch(() => setting.value?.value, (stored) => {
    if (draft.value !== null && stored === draft.value) {
      draft.value = null
    }
  })

  /**
   * Hands the edit to the caller's save. No-op when the row does not exist or
   * nothing changed.
   *
   * @param save - Emits the id and value to whoever persists them.
   */
  function submit(save: (id: number, value: string) => void): void {
    if (!setting.value || !dirty.value || draft.value === null) return
    save(setting.value.id, draft.value)
  }

  return { setting, value, dirty, submit }
}
