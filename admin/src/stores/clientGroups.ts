/**
 * Pinia store for the client groups staff put clients into.
 *
 * Owns the request and the sentence for a refusal, so the screens that offer a group to pick
 * hold no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientGroupsApi } from '@/composables/apis/useClientGroupsApi'
import { runAction } from '@/lib/runAction'
import type { ClientGroup } from '@/types/clientgroup'

/** The client groups and the state of loading them. */
export const useClientGroupsStore = defineStore('clientGroups', () => {
  const api = useClientGroupsApi()

  /** Every client group. */
  const groups = ref<ClientGroup[]>([])

  /** True while the groups are being loaded. */
  const loading = ref(false)

  /** Sentence for the last failed call, null when none. */
  const error = ref<string | null>(null)

  /**
   * Loads the client groups.
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

  return { groups, loading, error, fetchAll }
})
