/**
 * Endpoints behind the product bundles store.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { ProductBundle, ProductBundlePayload } from '@/types/productbundle'

/**
 * Endpoint functions for `/products/bundles`.
 *
 * @returns One typed function per endpoint.
 */
export const useProductBundlesApi = () => {
  const { request } = useApi()

  /**
   * Lists every product bundle, the ones switched off included.
   *
   * @returns The bundles.
   */
  const listBundles = (): Promise<ProductBundle[]> => request<ProductBundle[]>(ApiPath.ProductBundles)

  /**
   * Creates a product bundle; a new bundle is always active.
   *
   * @param payload - The bundle's fields and components.
   * @returns The new bundle's id.
   */
  const createBundle = (payload: ProductBundlePayload): Promise<number> =>
    request<number>(ApiPath.ProductBundles, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a bundle's fields and components.
   *
   * @param id - The bundle id.
   * @param payload - The bundle's new fields and components.
   * @returns Nothing.
   */
  const updateBundle = (id: number, payload: ProductBundlePayload): Promise<void> =>
    request<void>(`${ApiPath.ProductBundles}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a bundle; orders placed from it are kept and lose the link.
   *
   * @param id - The bundle id.
   * @returns Nothing.
   */
  const removeBundle = (id: number): Promise<void> =>
    request<void>(`${ApiPath.ProductBundles}/${id}`, { method: HttpMethod.Delete })

  return { listBundles, createBundle, updateBundle, removeBundle }
}
