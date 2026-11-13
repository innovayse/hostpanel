/**
 * Registry of the unsaved settings drafts on one page.
 *
 * A page that groups many settings controls provides a registry; every {@link useSettingDraft}
 * created below it registers itself, so the page can show how many edits are unsaved and save or
 * discard all of them from one sticky bar. A draft created outside such a page registers nowhere
 * and behaves exactly as before.
 */
import { computed, hasInjectionContext, inject, provide, shallowRef } from 'vue'
import type { ComputedRef, InjectionKey, Ref } from 'vue'
import type { SettingSave } from '@/types/setting'

/** What a draft hands the registry so the page can act on it. */
export interface RegisteredDraft {
  /** Whether the draft has an edit worth saving. */
  dirty: ComputedRef<boolean>
  /** True while the draft's own save is in flight. */
  saving: Ref<boolean>
  /** Saves the draft's edit through the page's saver. */
  submit: (save: SettingSave) => Promise<void>
  /** Drops the draft's edit, so the control shows the stored value again. */
  discard: () => void
}

/** The page-level view over all registered drafts. */
export interface SettingsDraftRegistry {
  /** Number of drafts with an unsaved edit. */
  dirtyCount: ComputedRef<number>
  /** True while any draft's save is in flight. */
  saving: ComputedRef<boolean>
  /** Adds a draft; returns the function that removes it again. */
  register: (draft: RegisteredDraft) => () => void
  /** Saves every draft that has an edit. */
  saveAll: (save: SettingSave) => Promise<void>
  /** Drops every unsaved edit. */
  discardAll: () => void
}

/** Injection key of the page's registry. */
const REGISTRY_KEY: InjectionKey<SettingsDraftRegistry> = Symbol('settingsDraftRegistry')

/**
 * Creates the registry and provides it to the components below the caller.
 *
 * @returns The registry, for the page's own save bar.
 */
export const provideSettingsDraftRegistry = (): SettingsDraftRegistry => {
  /** Registered drafts; replaced on every change so the computeds below re-run. */
  const drafts = shallowRef<RegisteredDraft[]>([])

  /** Number of drafts with an unsaved edit. */
  const dirtyCount = computed<number>(() => drafts.value.filter((d) => d.dirty.value).length)

  /** True while any draft's save is in flight. */
  const saving = computed<boolean>(() => drafts.value.some((d) => d.saving.value))

  /**
   * Adds a draft to the registry.
   *
   * @param draft - The draft to track.
   * @returns The function that removes it.
   */
  const register = (draft: RegisteredDraft): (() => void) => {
    drafts.value = [...drafts.value, draft]
    return () => {
      drafts.value = drafts.value.filter((d) => d !== draft)
    }
  }

  /**
   * Saves every dirty draft; each one keeps its own outcome next to its control.
   *
   * @param save - Persists one row and answers how it went.
   */
  const saveAll = async (save: SettingSave): Promise<void> => {
    await Promise.all(drafts.value.filter((d) => d.dirty.value).map((d) => d.submit(save)))
  }

  /** Drops the edit of every draft. */
  const discardAll = (): void => {
    drafts.value.forEach((d) => d.discard())
  }

  const registry: SettingsDraftRegistry = { dirtyCount, saving, register, saveAll, discardAll }
  provide(REGISTRY_KEY, registry)
  return registry
}

/**
 * Finds the registry of the page the caller sits on.
 *
 * @returns The registry, or null when the caller is outside a page that provides one.
 */
export const injectSettingsDraftRegistry = (): SettingsDraftRegistry | null =>
  hasInjectionContext() ? inject(REGISTRY_KEY, null) : null
