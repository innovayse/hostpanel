/**
 * Endpoints of the product form that no store owns: the defaults of the free-trial settings.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { ProductTrialDefaults } from '@/types/product'

/**
 * Endpoint functions for `/products`.
 *
 * @returns One typed function per endpoint.
 */
export const useProductsApi = () => {
  const { request } = useApi()

  /**
   * Reads the defaults and limits of the free-trial settings.
   *
   * @returns The defaults a new product starts with and the ranges the API accepts.
   */
  const getTrialDefaults = (): Promise<ProductTrialDefaults> =>
    request<ProductTrialDefaults>(`${ApiPath.Products}/trial-defaults`)

  return { getTrialDefaults }
}
