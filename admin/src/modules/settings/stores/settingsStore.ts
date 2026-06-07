import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Setting, EmailTemplate, Product, ProductGroup, CreateProductPayload, UpdateProductPayload, Gateway } from '../../../types/models'

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

  /** Product groups list. */
  const productGroups = ref<ProductGroup[]>([])

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
   * Fetches all products from the backend (including inactive).
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchProducts(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      products.value = await request<Product[]>('/products?activeOnly=false')
    } catch {
      error.value = 'Failed to load products.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches all product groups from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchProductGroups(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      productGroups.value = await request<ProductGroup[]>('/products/groups?activeOnly=false')
    } catch {
      error.value = 'Failed to load product groups.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new product.
   *
   * @param payload - Product creation data.
   * @returns Promise resolving to the new product ID.
   */
  async function createProduct(payload: CreateProductPayload): Promise<number> {
    const id = await request<number>('/products', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    await fetchProducts()
    return id
  }

  /**
   * Updates an existing product.
   *
   * @param id - Product ID.
   * @param payload - Updated product data.
   * @returns Promise that resolves when update is complete.
   */
  async function updateProduct(id: number, payload: UpdateProductPayload): Promise<void> {
    await request(`/products/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ id, ...payload }),
    })
    await fetchProducts()
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
    settings, emailTemplates, products, productGroups, gateways,
    loading, error,
    fetchSettings, fetchEmailTemplates, fetchProducts, fetchProductGroups,
    createProduct, updateProduct, fetchGateways,
  }
})
