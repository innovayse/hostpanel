/**
 * Endpoints behind the knowledge base store: categories and articles.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import type { KbArticle, KbArticlePayload, KbArticleUpdatePayload, KbCategory, KbCategoryPayload } from '@/types/kbarticle'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/admin/knowledgebase`.
 *
 * @returns One typed function per endpoint.
 */
export const useKnowledgebaseApi = () => {
  const { request } = useApi()

  /**
   * Lists the categories with their article counts.
   *
   * @returns Every category.
   */
  const listCategories = (): Promise<KbCategory[]> =>
    request<KbCategory[]>(ApiPath.AdminKnowledgebaseCategories)

  /**
   * Creates a category.
   *
   * @param payload - Name, description and hidden flag.
   * @returns The new category's id.
   */
  const createCategory = (payload: KbCategoryPayload): Promise<number> =>
    request<number>(ApiPath.AdminKnowledgebaseCategories, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a category.
   *
   * @param id - Category id.
   * @param payload - Name, description and hidden flag.
   * @returns Nothing.
   */
  const updateCategory = (id: number, payload: KbCategoryPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminKnowledgebaseCategories}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a category.
   *
   * @param id - Category id.
   * @returns Nothing.
   */
  const deleteCategory = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminKnowledgebaseCategories}/${id}`, { method: HttpMethod.Delete })

  /**
   * Lists every article, unpublished ones included.
   *
   * @returns Every article.
   */
  const listArticles = (): Promise<KbArticle[]> => request<KbArticle[]>(ApiPath.AdminKnowledgebase)

  /**
   * Creates an article.
   *
   * @param payload - Category id, translations and the published flag.
   * @returns The new article's id.
   */
  const createArticle = (payload: KbArticlePayload): Promise<number> =>
    request<number>(ApiPath.AdminKnowledgebase, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces the category and the translations of an article; its slug stays.
   *
   * @param id - Article id.
   * @param payload - Category id and translations.
   * @returns Nothing.
   */
  const updateArticle = (id: number, payload: KbArticleUpdatePayload): Promise<void> =>
    request<void>(`${ApiPath.AdminKnowledgebase}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes an article.
   *
   * @param id - Article id.
   * @returns Nothing.
   */
  const deleteArticle = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminKnowledgebase}/${id}`, { method: HttpMethod.Delete })

  return { listCategories, createCategory, updateCategory, deleteCategory, listArticles, createArticle, updateArticle, deleteArticle }
}
