import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Setting, EmailTemplate, Product, Gateway } from '../../../types/models'

/**
 * Pinia store for admin settings, email templates, products, and gateways.
 */
export const useSettingsStore = defineStore('settings', () => {
  const { request } = useApi()

  /** System settings list. */
  const settings = ref<Setting[]>([])

  /** Email templates list. */
  const emailTemplates = ref<EmailTemplate[]>([])

  /** Products list. */
  const products = ref<Product[]>([])

  /** Payment gateways list. */
  const gateways = ref<Gateway[]>([])

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches system settings from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchSettings(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      settings.value = await request<Setting[]>('/admin/settings')
    } catch {
      error.value = 'Failed to load settings.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches email templates from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchEmailTemplates(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      emailTemplates.value = await request<EmailTemplate[]>('/admin/email-templates')
    } catch {
      error.value = 'Failed to load email templates.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches products from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchProducts(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<{ items: Product[] }>('/products')
      products.value = result.items
    } catch {
      error.value = 'Failed to load products.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches payment gateways from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchGateways(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      gateways.value = await request<Gateway[]>('/admin/payment-gateways')
    } catch {
      error.value = 'Failed to load payment gateways.'
    } finally {
      loading.value = false
    }
  }

  return {
    settings, emailTemplates, products, gateways,
    loading, error,
    fetchSettings, fetchEmailTemplates, fetchProducts, fetchGateways,
  }
})
