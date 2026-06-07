/**
 * Pinia store for managing homepage slides.
 *
 * Provides CRUD operations and sort order management
 * for the admin slides settings page.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Slide, CreateSlidePayload, UpdateSlidePayload } from '../../../types/models'

/** Manages homepage slides state and CRUD operations. */
export const useSlidesStore = defineStore('slides', () => {
  const { request } = useApi()

  /** All slides from the admin API. */
  const slides = ref<Slide[]>([])

  /** True while any operation is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all slides from the admin API.
   *
   * @returns Promise that resolves when slides are loaded.
   */
  async function fetchSlides(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      slides.value = await request<Slide[]>('/admin/slides')
    } catch {
      error.value = 'Failed to load slides.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new slide and refreshes the list.
   *
   * @param payload - Slide data with translations.
   * @returns The new slide's ID.
   */
  async function createSlide(payload: CreateSlidePayload): Promise<number> {
    const id = await request<number>('/admin/slides', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    await fetchSlides()
    return id
  }

  /**
   * Updates an existing slide and refreshes the list.
   *
   * @param id - Slide ID.
   * @param payload - Updated slide data with translations.
   */
  async function updateSlide(id: number, payload: UpdateSlidePayload): Promise<void> {
    await request(`/admin/slides/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await fetchSlides()
  }

  /**
   * Deletes a slide and refreshes the list.
   *
   * @param id - Slide ID to delete.
   */
  async function deleteSlide(id: number): Promise<void> {
    await request(`/admin/slides/${id}`, {
      method: 'DELETE',
    })
    await fetchSlides()
  }

  /**
   * Updates sort order for multiple slides.
   *
   * @param items - Array of id + sortOrder pairs.
   */
  async function updateOrder(items: { id: number; sortOrder: number }[]): Promise<void> {
    await request('/admin/slides/order', {
      method: 'PUT',
      body: JSON.stringify({ items }),
    })
    await fetchSlides()
  }

  /**
   * Fetches a single slide by ID.
   *
   * Returns from the local cache if available, otherwise fetches all slides first.
   *
   * @param id - Slide ID to look up.
   * @returns The matching slide, or undefined if not found.
   */
  async function fetchSlide(id: number): Promise<Slide | undefined> {
    if (slides.value.length > 0) {
      return slides.value.find(s => s.id === id)
    }
    await fetchSlides()
    return slides.value.find(s => s.id === id)
  }

  return { slides, loading, error, fetchSlides, fetchSlide, createSlide, updateSlide, deleteSlide, updateOrder }
})
