/**
 * Endpoints behind the TLD configs store: TLD pricing CRUD, registrar import and the manual
 * cost-price sync.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type {
  CreateTldConfigPayload,
  TldConfig,
  TldConfigFilter,
  TldConfigListItem,
  TldImportResult,
  UpdateTldConfigPayload,
} from '@/types/tldconfig'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/admin/tld-configs`.
 *
 * @returns One typed function per endpoint.
 */
export const useTldConfigsApi = () => {
  const { request } = useApi()

  /**
   * Lists TLD configs, one page.
   *
   * @param filter - Page, page size, TLD search, registrar module and enabled state.
   * @returns The page and the total count.
   */
  const listTldConfigs = (filter: TldConfigFilter): Promise<PaginatedList<TldConfigListItem>> =>
    request<PaginatedList<TldConfigListItem>>(
      `${ApiPath.AdminTldConfigs}${buildQuery({
        page: filter.page,
        pageSize: filter.pageSize,
        search: filter.search,
        registrarModule: filter.registrarModule,
        enabled: filter.enabled,
      })}`,
    )

  /**
   * Reads one TLD config.
   *
   * @param id - Config id.
   * @returns The full config.
   */
  const getTldConfig = (id: number): Promise<TldConfig> =>
    request<TldConfig>(`${ApiPath.AdminTldConfigs}/${id}`)

  /**
   * Creates a TLD config.
   *
   * @param payload - Config data.
   * @returns The new config's id.
   */
  const createTldConfig = (payload: CreateTldConfigPayload): Promise<number> =>
    request<number>(ApiPath.AdminTldConfigs, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a TLD config.
   *
   * @param id - Config id.
   * @param payload - Updated config data.
   * @returns Nothing; the API answers 204.
   */
  const updateTldConfig = (id: number, payload: UpdateTldConfigPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminTldConfigs}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a TLD config.
   *
   * @param id - Config id.
   * @returns Nothing; the API answers 204.
   */
  const deleteTldConfig = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminTldConfigs}/${id}`, { method: HttpMethod.Delete })

  /**
   * Imports TLDs from a registrar module.
   *
   * @param module - Registrar module name.
   * @returns Counts of what was imported.
   */
  const importTldConfigs = (module: string): Promise<TldImportResult> =>
    request<TldImportResult>(`${ApiPath.AdminTldConfigs}/import/${module}`, { method: HttpMethod.Post })

  /**
   * Triggers a manual cost-price sync from every provider.
   *
   * @returns Nothing; the API answers 204.
   */
  const syncTldPrices = (): Promise<void> =>
    request<void>(`${ApiPath.AdminTldConfigs}/sync`, { method: HttpMethod.Post })

  return {
    listTldConfigs,
    getTldConfig,
    createTldConfig,
    updateTldConfig,
    deleteTldConfig,
    importTldConfigs,
    syncTldPrices,
  }
}
