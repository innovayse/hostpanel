/**
 * Draft-then-save state for one settings row, by key.
 *
 * Every settings control on the System Settings page follows the same rule: show the operator's
 * unsaved edit when there is one, the stored value otherwise, and clear the edit only when the
 * stored value catches up, never on the save request. The row also carries its own save state
 * (in flight, and how the last save went), so a card shows the outcome next to the control that
 * was saved. On a page that provides a settings draft registry the draft also registers there, so
 * the page's save bar can count, save and discard the edits of every control together.
 */
import { computed, onScopeDispose, ref, watch } from 'vue'
import type { ComputedRef, Ref, WritableComputedRef } from 'vue'
import { injectSettingsDraftRegistry } from '@/composables/useSettingsDraftRegistry'
import type { Setting, SettingSave, SettingSaveResult } from '@/types/setting'

/** What {@link useSettingDraft} hands back for one settings row. */
export interface SettingDraft {
  /** The stored row, or undefined when the key was never seeded. */
  setting: ComputedRef<Setting | undefined>
  /** What the control shows and edits: the edit, else the stored value, else the fallback. */
  value: WritableComputedRef<string>
  /** Whether there is an edit worth saving. */
  dirty: ComputedRef<boolean>
  /** True while this row's save is in flight. */
  saving: Ref<boolean>
  /** How this row's last save went; null before the first save and after the next edit. */
  outcome: Ref<SettingSaveResult | null>
  /** Hands the edit to the caller's save and records the outcome; a no-op when the row is missing, nothing changed or a save is in flight. */
  submit: (save: SettingSave) => Promise<void>
  /** Drops the edit, so the control shows the stored value again. */
  discard: () => void
}

/**
 * Creates the draft state of one settings row.
 *
 * The edit is dropped only when the stored value equals it. Clearing it on the save request would
 * discard the operator's pick before anyone knew whether it had been written, so a failed save
 * would snap the control back to the value already in use.
 *
 * @param settings - All system settings, as loaded from the backend.
 * @param key - The settings key this control edits.
 * @param fallback - Shown when the row exists but is empty, or does not exist.
 * @returns The row, the editable value, whether it changed, and a saver.
 */
export const useSettingDraft = (settings: Ref<Setting[]>, key: string, fallback = ''): SettingDraft => {
  /** The stored row, or undefined when the key was never seeded. */
  const setting = computed(() => settings.value.find((s) => s.key === key))

  /** The operator's unsaved edit; null when there is none. */
  const draft = ref<string | null>(null)

  /** True while this row's save is in flight. */
  const saving = ref(false)

  /** How the last save of this row went; null when it has not been saved since the last edit. */
  const outcome = ref<SettingSaveResult | null>(null)

  /** What the control shows: the edit, else the stored value, else the fallback. */
  const value = computed<string>({
    /**
     * The text the control shows.
     *
     * @returns The edit, else the stored value, else the fallback.
     */
    get: () => draft.value ?? (setting.value?.value || fallback),
    /**
     * Records an edit.
     *
     * @param next - The text the operator typed or picked.
     */
    set: (next: string) => {
      draft.value = next
      outcome.value = null
    },
  })

  /** Whether there is an edit worth saving. */
  const dirty = computed<boolean>(() => draft.value !== null && draft.value !== (setting.value?.value ?? ''))

  // The edit is dropped once the stored value catches up with it, and not before.
  watch(() => setting.value?.value, (stored) => {
    if (draft.value !== null && stored === draft.value) {
      draft.value = null
    }
  })

  /**
   * Hands the edit to the caller's save and keeps the outcome. No-op when the row does not exist,
   * nothing changed, or this row is already being saved.
   *
   * @param save - Persists the id and value and answers how it went.
   */
  const submit = async (save: SettingSave): Promise<void> => {
    if (!setting.value || !dirty.value || draft.value === null || saving.value) {
      return
    }
    outcome.value = null
    saving.value = true
    outcome.value = await save(setting.value.id, draft.value)
    saving.value = false
  }

  /** Drops the edit, so the control shows the stored value again. */
  const discard = (): void => {
    draft.value = null
    outcome.value = null
  }

  const registry = injectSettingsDraftRegistry()
  if (registry) {
    onScopeDispose(registry.register({ dirty, saving, submit, discard }))
  }

  return { setting, value, dirty, saving, outcome, submit, discard }
}
