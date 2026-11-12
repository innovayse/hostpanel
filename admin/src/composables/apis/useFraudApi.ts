/**
 * One function per endpoint on the fraud-detection surface: the block lists
 * (`/admin/fraud/blocklist`), the fraud log (`/admin/fraud/log`) and the fraud settings
 * (`/admin/fraud/settings`). No state lives here, no error handling.
 *
 * The callers are the fraud stores, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { AddBlockListEntryPayload, BlockListEntry, BlockListType, FraudLogEntry, FraudLogFilters, FraudSettings } from '@/types/fraud'
import type { PaginatedList } from '@/types/paginatedlist'

/**
 * Endpoint functions for the fraud-detection surface.
 *
 * @returns One typed function per endpoint the fraud screens use.
 */
export const useFraudApi = () => {
  const { request } = useApi()

  /**
   * Lists one page of a block list, newest first.
   *
   * @param type - Which block list to read.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @returns One page of entries.
   */
  const listBlockList = (type: BlockListType, page: number, pageSize: number): Promise<PaginatedList<BlockListEntry>> =>
    request<PaginatedList<BlockListEntry>>(`${ApiPath.AdminFraudBlockList}${buildQuery({ page, pageSize, type })}`)

  /**
   * Adds an entry to a block list.
   *
   * @param payload - The type, the value to block and an optional note.
   * @returns The id of the new entry.
   */
  const addBlockListEntry = (payload: AddBlockListEntryPayload): Promise<number> =>
    request<number>(ApiPath.AdminFraudBlockList, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Removes an entry from its block list.
   *
   * @param id - Entry id.
   * @returns Nothing; the API answers 204.
   */
  const deleteBlockListEntry = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminFraudBlockList}/${id}`, { method: HttpMethod.Delete })

  /**
   * Lists one page of the fraud log, newest first.
   *
   * @param filters - Decision and reason; empty values are not sent.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @returns One page of log entries.
   */
  const listLog = (filters: FraudLogFilters, page: number, pageSize: number): Promise<PaginatedList<FraudLogEntry>> =>
    request<PaginatedList<FraudLogEntry>>(
      `${ApiPath.AdminFraudLog}${buildQuery({ page, pageSize, decision: filters.decision, reason: filters.reason })}`,
    )

  /**
   * Reads the fraud settings.
   *
   * @returns The stored settings.
   */
  const getSettings = (): Promise<FraudSettings> => request<FraudSettings>(ApiPath.AdminFraudSettings)

  /**
   * Replaces the fraud settings.
   *
   * @param settings - The settings to store.
   * @returns Nothing; the API answers 204.
   */
  const updateSettings = (settings: FraudSettings): Promise<void> =>
    request<void>(ApiPath.AdminFraudSettings, { method: HttpMethod.Put, body: JSON.stringify(settings) })

  return { listBlockList, addBlockListEntry, deleteBlockListEntry, listLog, getSettings, updateSettings }
}
