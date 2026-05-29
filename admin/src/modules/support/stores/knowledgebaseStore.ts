import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { KbCategory, KbArticle } from '../../../types/models'

/**
 * Pinia store for managing knowledge base categories and articles.
 *
 * Provides CRUD for categories and articles in the admin panel.
 */
export const useKnowledgebaseStore = defineStore('knowledgebase', () => {
  const { request } = useApi()

  /** List of KB categories. */
  const categories = ref<KbCategory[]>([])

  /** List of KB articles. */
  const articles = ref<KbArticle[]>([])

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all knowledge base categories with article counts.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchCategories(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      categories.value = await request<KbCategory[]>('/admin/knowledgebase/categories')
    } catch {
      error.value = 'Failed to load categories.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new KB category.
   *
   * @param payload - Category data.
   * @returns The new category ID, or null on failure.
   */
  async function createCategory(payload: {
    name: string
    description: string
    isHidden: boolean
  }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const id = await request<number>('/admin/knowledgebase/categories', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      await fetchCategories()
      return id
    } catch {
      error.value = 'Failed to create category.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates an existing KB category.
   *
   * @param id - The category identifier.
   * @param payload - Updated data.
   * @returns True on success.
   */
  async function updateCategory(id: number, payload: {
    name: string
    description: string
    isHidden: boolean
  }): Promise<boolean> {
    error.value = null
    try {
      await request(`/admin/knowledgebase/categories/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      await fetchCategories()
      return true
    } catch {
      error.value = 'Failed to update category.'
      return false
    }
  }

  /**
   * Deletes a KB category.
   *
   * @param id - The category identifier.
   * @returns True on success.
   */
  async function deleteCategory(id: number): Promise<boolean> {
    error.value = null
    try {
      await request(`/admin/knowledgebase/categories/${id}`, { method: 'DELETE' })
      await fetchCategories()
      return true
    } catch {
      error.value = 'Failed to delete category.'
      return false
    }
  }

  /**
   * Fetches all KB articles (including unpublished).
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchArticles(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      articles.value = await request<KbArticle[]>('/admin/knowledgebase')
    } catch {
      error.value = 'Failed to load articles.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new KB article.
   *
   * @param payload - Article data.
   * @returns The new article ID, or null on failure.
   */
  async function createArticle(payload: {
    title: string
    content: string
    category: string
  }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      return await request<number>('/admin/knowledgebase', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to create article.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes a KB article.
   *
   * @param id - The article identifier.
   * @returns True on success.
   */
  async function deleteArticle(id: number): Promise<boolean> {
    error.value = null
    try {
      await request(`/admin/knowledgebase/${id}`, { method: 'DELETE' })
      return true
    } catch {
      error.value = 'Failed to delete article.'
      return false
    }
  }

  return {
    categories,
    articles,
    loading,
    error,
    fetchCategories,
    createCategory,
    updateCategory,
    deleteCategory,
    fetchArticles,
    createArticle,
    deleteArticle,
  }
})
