/**
 * Pinia store for the client groups staff put clients into.
 *
 * Owns the group list the Client groups screen and the group pickers read, and the create, update,
 * set-default and delete calls. It is the only caller of `useClientGroupsApi`; a refusal becomes
 * the API sentence in the matching error ref (`error`, `saveError`, `actionError`), so no view
 * holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientGroupsApi } from '@/composables/apis/useClientGroupsApi'
import { runAction } from '@/lib/runAction'
import type { ClientGroup, ClientGroupPayload } from '@/types/clientgroup'

/** Lists, creates, updates, deletes and sets the default of client groups for the admin screens. */
export const useClientGroupsStore = defineStore('clientGroups', () => {
  const api = useClientGroupsApi()

  /** Loaded client groups. */
  const groups = ref<ClientGroup[]>([])

  /** True while the list is loading. */
  const loading = ref(false)

  /** True while a create or update is in flight. */
  const saving = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused create or update, null when none. */
  const saveError = ref<string | null>(null)

  /** Per-field messages of the last refused create or update, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Sentence for the last refused set-default or delete, null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Re-reads the list after a write, so the screen shows what the API now holds.
   *
   * @returns Promise that resolves when the list is replaced; rejects with the API refusal.
   */
  const reload = async (): Promise<void> => {
    groups.value = await api.listGroups()
  }

  /**
   * Loads every group, replacing the list.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, api.listGroups)
    loading.value = false
    if (result.ok) {
      groups.value = result.value
    }
    return result.ok
  }

  /**
   * Clears the form refusal, so a previous dialog's messages do not show on the next one.
   *
   * @returns Nothing.
   */
  const resetSaveError = (): void => {
    saveError.value = null
    fieldErrors.value = {}
  }

  /**
   * Clears the set-default and delete refusal, so a previous visit's sentence does not show on the list.
   *
   * @returns Nothing.
   */
  const clearActionError = (): void => {
    actionError.value = null
  }

  /**
   * Creates a group, then re-reads the list.
   *
   * @param payload - The group's fields.
   * @returns True when created; false when refused (see `saveError`, `fieldErrors`).
   */
  const create = async (payload: ClientGroupPayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(
      saveError,
      async () => {
        await api.createGroup(payload)
        await reload()
      },
      fieldErrors,
    )
    saving.value = false
    return result.ok
  }

  /**
   * Replaces a group's fields, then re-reads the list.
   *
   * @param id - The group id.
   * @param payload - The group's new fields.
   * @returns True when saved; false when refused (see `saveError`, `fieldErrors`).
   */
  const update = async (id: number, payload: ClientGroupPayload): Promise<boolean> => {
    saving.value = true
    const result = await runAction(
      saveError,
      async () => {
        await api.updateGroup(id, payload)
        await reload()
      },
      fieldErrors,
    )
    saving.value = false
    return result.ok
  }

  /**
   * Makes a group the default for new clients, or clears the flag, then re-reads the list.
   *
   * @param id - The group id.
   * @param isDefault - True to make it the default; false to clear the flag.
   * @returns True when written; false when refused (see `actionError`).
   */
  const setDefault = async (id: number, isDefault: boolean): Promise<boolean> => {
    const result = await runAction(actionError, async () => {
      await api.setDefaultGroup(id, isDefault)
      await reload()
    })
    return result.ok
  }

  /**
   * Deletes a group that has no clients, then re-reads the list.
   *
   * @param id - The group id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, async () => {
      await api.removeGroup(id)
      await reload()
    })
    return result.ok
  }

  return {
    groups,
    loading,
    saving,
    error,
    saveError,
    fieldErrors,
    actionError,
    fetchAll,
    resetSaveError,
    clearActionError,
    create,
    update,
    setDefault,
    remove,
  }
})
