/**
 * Pinia store for provisioning servers and server groups.
 *
 * Servers are read a page at a time for the list screen and as one unpaged lean lookup for the screens that choose a server
 * or sum up all of them; groups are always read whole. A write does not read a list again: the screen that wrote reloads
 * the page the address names. The only caller of `useServersApi`; a refusal is kept as the API's own sentence, never thrown.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useServersApi } from '@/composables/apis/useServersApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type {
  ServerDto,
  ServerGroupDto,
  ServerGroupPayload,
  ServerListFilters,
  ServerModule,
  ServerOptionDto,
  ServerPayload,
  TestConnectionResultDto,
} from '@/types/server'

/** Manages all provisioning server and server group state for the admin panel. */
export const useServersStore = defineStore('servers', () => {
  const api = useServersApi()

  /** The servers of the loaded page. */
  const servers = ref<ServerDto[]>([])

  /** Items per page; the store owns it so a screen never names it. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching servers across all pages. */
  const totalCount = ref(0)

  /** Every server as a lean option, for the screens that choose a server or sum up all of them. */
  const serverOptions = ref<ServerOptionDto[]>([])

  /** All server groups with their assigned servers. */
  const groups = ref<ServerGroupDto[]>([])

  /** True while a page of servers or the groups are being read. */
  const loading = ref(false)

  /** True while the option lookup is being read, so a picker opening never turns the list screen into placeholders. */
  const optionsLoading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed create, update, delete or connection test, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field sentences of the last refused create or update (camelCase keys), empty when none. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads one page of the server list. The page, the search and the filters come from the list's address, so the store
   * keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text, the module and the last known state; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchServers = async (page: number, filters: ServerListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listServers({ ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      servers.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Loads every server as a lean option into `serverOptions`, for a picker or a summary of all servers.
   *
   * @param module - Keep only the servers that run this module; every server when omitted.
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchServerOptions = async (module?: ServerModule): Promise<void> => {
    optionsLoading.value = true
    const result = await runAction(error, () => api.listServerOptions(module))
    optionsLoading.value = false
    if (result.ok) {
      serverOptions.value = result.value
    }
  }

  /**
   * Loads all server groups from the API.
   *
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const fetchGroups = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, api.listServerGroups)
    loading.value = false
    if (result.ok) {
      groups.value = result.value
    }
  }

  /**
   * Runs one mutating request under the action error and the field errors.
   *
   * @param task - The request to run.
   * @returns True when the API accepted it; false when refused (see `actionError`).
   */
  const mutate = async (task: () => Promise<void>): Promise<boolean> => {
    const result = await runAction(actionError, task, fieldErrors)
    return result.ok
  }

  /**
   * Creates a new server.
   *
   * @param payload - Server creation data.
   * @returns True when created; false when refused (see `actionError`).
   */
  const createServer = (payload: ServerPayload): Promise<boolean> => mutate(() => api.createServer(payload))

  /**
   * Updates an existing server.
   *
   * @param id - Server identifier.
   * @param payload - Updated server data.
   * @returns True when updated; false when refused (see `actionError`).
   */
  const updateServer = (id: number, payload: ServerPayload): Promise<boolean> =>
    mutate(() => api.updateServer(id, payload))

  /**
   * Deletes a server.
   *
   * @param id - Server identifier.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const deleteServer = (id: number): Promise<boolean> => mutate(() => api.deleteServer(id))

  /**
   * Tests the connection to a server and records its status.
   *
   * @param id - Server identifier.
   * @returns The test result DTO, or null when the request itself was refused (see `actionError`).
   */
  const testConnection = async (id: number): Promise<TestConnectionResultDto | null> => {
    const result = await runAction(actionError, () => api.testServerConnection(id))
    return result.ok ? result.value : null
  }

  /**
   * Creates a new server group.
   *
   * @param payload - Group creation data.
   * @returns True when created; false when refused (see `actionError`).
   */
  const createGroup = (payload: ServerGroupPayload): Promise<boolean> =>
    mutate(() => api.createServerGroup(payload))

  /**
   * Updates an existing server group.
   *
   * @param id - Group identifier.
   * @param payload - Updated group data.
   * @returns True when updated; false when refused (see `actionError`).
   */
  const updateGroup = (id: number, payload: ServerGroupPayload): Promise<boolean> =>
    mutate(() => api.updateServerGroup(id, payload))

  /**
   * Deletes a server group.
   *
   * @param id - Group identifier.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const deleteGroup = (id: number): Promise<boolean> => mutate(() => api.deleteServerGroup(id))

  return {
    servers,
    pageSize,
    totalCount,
    serverOptions,
    groups,
    loading,
    optionsLoading,
    error,
    actionError,
    fieldErrors,
    fetchServers,
    fetchServerOptions,
    fetchGroups,
    createServer,
    updateServer,
    deleteServer,
    testConnection,
    createGroup,
    updateGroup,
    deleteGroup,
  }
})
