/**
 * Pinia store for knowledge base categories and articles in the admin panel.
 *
 * Owns every request the knowledge base page makes and the sentence for a refusal, so the
 * page holds no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useKnowledgebaseApi } from '@/composables/apis/useKnowledgebaseApi'
import { runAction } from '@/lib/runAction'
import type { KbArticle, KbArticlePayload, KbArticleUpdatePayload, KbCategory, KbCategoryPayload } from '@/types/kbarticle'

/** Loads and changes knowledge base categories and articles. */
export const useKnowledgebaseStore = defineStore('knowledgebase', () => {
  const api = useKnowledgebaseApi()

  /** List of KB categories. */
  const categories = ref<KbCategory[]>([])

  /** List of KB articles. */
  const articles = ref<KbArticle[]>([])

  /** True while the categories load or one of them is being created. */
  const loading = ref(false)

  /** True while the articles load; apart from `loading`, so a slow list never hides the other. */
  const articlesLoading = ref(false)

  /** Sentence for the last failed call, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads every category with its article count.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchCategories = async (): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.listCategories())
    loading.value = false
    if (result.ok) {
      categories.value = result.value
    }
    return result.ok
  }

  /**
   * Creates a category, then reloads the list.
   *
   * @param payload - Category data.
   * @returns The new category's id, or null when refused (see `error` and `fieldErrors`).
   */
  const createCategory = async (payload: KbCategoryPayload): Promise<number | null> => {
    loading.value = true
    const result = await runAction(error, () => api.createCategory(payload), fieldErrors)
    loading.value = false
    if (!result.ok) {
      return null
    }
    await fetchCategories()
    return result.value
  }

  /**
   * Replaces a category, then reloads the list.
   *
   * @param id - The category identifier.
   * @param payload - Updated data.
   * @returns True when saved; false when refused (see `error` and `fieldErrors`).
   */
  const updateCategory = async (id: number, payload: KbCategoryPayload): Promise<boolean> => {
    const result = await runAction(error, () => api.updateCategory(id, payload), fieldErrors)
    if (result.ok) {
      await fetchCategories()
    }
    return result.ok
  }

  /**
   * Deletes a category, then reloads the list.
   *
   * @param id - The category identifier.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteCategory = async (id: number): Promise<boolean> => {
    const result = await runAction(error, () => api.deleteCategory(id))
    if (result.ok) {
      await fetchCategories()
    }
    return result.ok
  }

  /**
   * Loads every article, unpublished ones included.
   *
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchArticles = async (): Promise<boolean> => {
    articlesLoading.value = true
    const result = await runAction(error, () => api.listArticles())
    articlesLoading.value = false
    if (result.ok) {
      articles.value = result.value
    }
    return result.ok
  }

  /**
   * Creates an article, then reloads the list.
   *
   * @param payload - Article data.
   * @returns The new article's id, or null when refused (see `error` and `fieldErrors`).
   */
  const createArticle = async (payload: KbArticlePayload): Promise<number | null> => {
    const result = await runAction(error, () => api.createArticle(payload), fieldErrors)
    if (!result.ok) {
      return null
    }
    await fetchArticles()
    return result.value
  }

  /**
   * Replaces the category and translations of an article, then reloads the list.
   *
   * @param id - The article identifier.
   * @param payload - Updated category and translations.
   * @returns True when saved; false when refused (see `error` and `fieldErrors`).
   */
  const updateArticle = async (id: number, payload: KbArticleUpdatePayload): Promise<boolean> => {
    const result = await runAction(error, () => api.updateArticle(id, payload), fieldErrors)
    if (result.ok) {
      await fetchArticles()
    }
    return result.ok
  }

  /**
   * Deletes an article.
   *
   * @param id - The article identifier.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteArticle = async (id: number): Promise<boolean> =>
    (await runAction(error, () => api.deleteArticle(id))).ok

  return {
    categories,
    articles,
    loading,
    articlesLoading,
    error,
    fieldErrors,
    fetchCategories,
    createCategory,
    updateCategory,
    deleteCategory,
    fetchArticles,
    createArticle,
    updateArticle,
    deleteArticle,
  }
})
