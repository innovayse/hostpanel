import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { PredefinedReplyCategory, PredefinedReply } from '../../../types/models'

/**
 * Pinia store for managing predefined replies and their categories.
 *
 * Provides CRUD for categories and replies, plus search functionality.
 */
export const usePredefinedRepliesStore = defineStore('predefinedReplies', () => {
  const { request } = useApi()

  /** List of reply categories. */
  const categories = ref<PredefinedReplyCategory[]>([])

  /** List of predefined replies. */
  const replies = ref<PredefinedReply[]>([])

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all predefined reply categories.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchCategories(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      categories.value = await request<PredefinedReplyCategory[]>('/predefined-replies/categories')
    } catch {
      error.value = 'Failed to load categories.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new category.
   *
   * @param name - The category name.
   * @returns The new category ID, or null on failure.
   */
  async function createCategory(name: string): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      const id = await request<number>('/predefined-replies/categories', {
        method: 'POST',
        body: JSON.stringify({ name }),
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
   * Deletes a category.
   *
   * @param id - The category identifier.
   * @returns True on success.
   */
  async function deleteCategory(id: number): Promise<boolean> {
    error.value = null
    try {
      await request(`/predefined-replies/categories/${id}`, { method: 'DELETE' })
      await fetchCategories()
      return true
    } catch {
      error.value = 'Failed to delete category.'
      return false
    }
  }

  /**
   * Fetches all predefined replies, optionally filtered by category.
   *
   * @param categoryId - Optional category ID filter.
   */
  async function fetchReplies(categoryId?: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = categoryId ? `?categoryId=${categoryId}` : ''
      replies.value = await request<PredefinedReply[]>(`/predefined-replies${params}`)
    } catch {
      error.value = 'Failed to load replies.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Searches predefined replies by query string.
   *
   * @param query - The search term.
   */
  async function searchReplies(query: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      replies.value = await request<PredefinedReply[]>(`/predefined-replies/search?q=${encodeURIComponent(query)}`)
    } catch {
      error.value = 'Search failed.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new predefined reply.
   *
   * @param payload - Reply data.
   * @returns The new reply ID, or null on failure.
   */
  async function createReply(payload: { name: string; content: string; categoryId: number }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      return await request<number>('/predefined-replies', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to create reply.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates an existing predefined reply.
   *
   * @param id - The reply identifier.
   * @param payload - Updated data.
   * @returns True on success.
   */
  async function updateReply(id: number, payload: { name: string; content: string; categoryId: number }): Promise<boolean> {
    error.value = null
    try {
      await request(`/predefined-replies/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      return true
    } catch {
      error.value = 'Failed to update reply.'
      return false
    }
  }

  /**
   * Deletes a predefined reply.
   *
   * @param id - The reply identifier.
   * @returns True on success.
   */
  async function deleteReply(id: number): Promise<boolean> {
    error.value = null
    try {
      await request(`/predefined-replies/${id}`, { method: 'DELETE' })
      return true
    } catch {
      error.value = 'Failed to delete reply.'
      return false
    }
  }

  return {
    categories,
    replies,
    loading,
    error,
    fetchCategories,
    createCategory,
    deleteCategory,
    fetchReplies,
    searchReplies,
    createReply,
    updateReply,
    deleteReply,
  }
})
