/**
 * Pinia store for knowledge base categories and articles in the admin panel.
 *
 * Owns every request the knowledge base page makes and the sentence for a refusal, so the
 * page holds no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useKnowledgebaseApi } from '@/composables/apis/useKnowledgebaseApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type {
  KbArticle,
  KbArticleListFilters,
  KbArticlePayload,
  KbArticleUpdatePayload,
  KbCategory,
  KbCategoryPayload,
} from '@/types/kbarticle'

/** Loads and changes knowledge base categories and articles. */
export const useKnowledgebaseStore = defineStore('knowledgebase', () => {
  const api = useKnowledgebaseApi()

  /** List of KB categories. */
  const categories = ref<KbCategory[]>([])

  /** The articles of the loaded page. */
  const articles = ref<KbArticle[]>([])

  /** Total number of matching articles across all pages. */
  const articlesTotalCount = ref(0)

  /** Articles per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** True while the categories load or one of them is being created. */
  const loading = ref(false)

  /** True while the articles load; apart from `loading`, so a slow list never hides the other. */
  const articlesLoading = ref(false)

  /** Sentence for the last failed call, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed articles load; apart from `error`, so the categories and the articles load side by side without one clearing the other's refusal. */
  const articlesError = ref<string | null>(null)

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
   * Loads one page of the articles, unpublished ones included, narrowed by the server. The page, the search and the
   * filters come from the list's address, so the store keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text, the category and the published state; a member left out does not narrow.
   * @returns True when loaded; false when refused (see `articlesError`).
   */
  const fetchArticles = async (page: number, filters: KbArticleListFilters = {}): Promise<boolean> => {
    articlesLoading.value = true
    const result = await runAction(articlesError, () =>
      api.listArticles({ ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    articlesLoading.value = false
    if (result.ok) {
      articles.value = result.value.items
      articlesTotalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Creates an article. The screen reads the list again, because only it knows the page and the filters.
   *
   * @param payload - Article data.
   * @returns The new article's id, or null when refused (see `error` and `fieldErrors`).
   */
  const createArticle = async (payload: KbArticlePayload): Promise<number | null> => {
    const result = await runAction(error, () => api.createArticle(payload), fieldErrors)
    return result.ok ? result.value : null
  }

  /**
   * Replaces the category and translations of an article. The screen reads the list again, because only it knows the
   * page and the filters.
   *
   * @param id - The article identifier.
   * @param payload - Updated category and translations.
   * @returns True when saved; false when refused (see `error` and `fieldErrors`).
   */
  const updateArticle = async (id: number, payload: KbArticleUpdatePayload): Promise<boolean> =>
    (await runAction(error, () => api.updateArticle(id, payload), fieldErrors)).ok

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
    articlesTotalCount,
    pageSize,
    loading,
    articlesLoading,
    error,
    articlesError,
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
