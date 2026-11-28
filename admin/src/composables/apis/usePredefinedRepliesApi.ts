/**
 * Endpoints behind the predefined replies store: reply categories and canned replies.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PredefinedReply, PredefinedReplyCategory, PredefinedReplyPayload } from '@/types/predefinedreply'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/predefined-replies`.
 *
 * @returns One typed function per endpoint.
 */
export const usePredefinedRepliesApi = () => {
  const { request } = useApi()

  /**
   * Lists the reply categories.
   *
   * @returns Every category.
   */
  const listCategories = (): Promise<PredefinedReplyCategory[]> =>
    request<PredefinedReplyCategory[]>(ApiPath.PredefinedRepliesCategories)

  /**
   * Creates a reply category.
   *
   * @param name - Category name.
   * @returns The new category's id.
   */
  const createCategory = (name: string): Promise<number> =>
    request<number>(ApiPath.PredefinedRepliesCategories, { method: HttpMethod.Post, body: JSON.stringify({ name }) })

  /**
   * Renames a reply category.
   *
   * @param id - Category id.
   * @param name - The new name.
   * @returns Nothing.
   */
  const updateCategory = (id: number, name: string): Promise<void> =>
    request<void>(`${ApiPath.PredefinedRepliesCategories}/${id}`, { method: HttpMethod.Put, body: JSON.stringify({ name }) })

  /**
   * Deletes a reply category.
   *
   * @param id - Category id.
   * @returns Nothing.
   */
  const deleteCategory = (id: number): Promise<void> =>
    request<void>(`${ApiPath.PredefinedRepliesCategories}/${id}`, { method: HttpMethod.Delete })

  /**
   * Lists replies, optionally of one category.
   *
   * @param categoryId - Category to keep; undefined for all.
   * @returns The replies.
   */
  const listReplies = (categoryId?: number): Promise<PredefinedReply[]> =>
    request<PredefinedReply[]>(`${ApiPath.PredefinedReplies}${buildQuery({ categoryId })}`)

  /**
   * Searches replies by name and content.
   *
   * @param query - The search term.
   * @returns The matching replies.
   */
  const searchReplies = (query: string): Promise<PredefinedReply[]> =>
    request<PredefinedReply[]>(`${ApiPath.PredefinedReplies}/search${buildQuery({ search: query })}`)

  /**
   * Creates a reply.
   *
   * @param payload - Name, content and category.
   * @returns The new reply's id.
   */
  const createReply = (payload: PredefinedReplyPayload): Promise<number> =>
    request<number>(ApiPath.PredefinedReplies, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a reply.
   *
   * @param id - Reply id.
   * @param payload - Name, content and category.
   * @returns Nothing.
   */
  const updateReply = (id: number, payload: PredefinedReplyPayload): Promise<void> =>
    request<void>(`${ApiPath.PredefinedReplies}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a reply.
   *
   * @param id - Reply id.
   * @returns Nothing.
   */
  const deleteReply = (id: number): Promise<void> =>
    request<void>(`${ApiPath.PredefinedReplies}/${id}`, { method: HttpMethod.Delete })

  return {
    listCategories, createCategory, updateCategory, deleteCategory,
    listReplies, searchReplies, createReply, updateReply, deleteReply,
  }
}
