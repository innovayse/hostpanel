/**
 * Pinia store for admin settings, email templates, products and product groups.
 *
 * The only caller of `useSettingsApi` and `useUploadApi` for these screens. A refusal is kept as
 * the API's own sentence in `error`, `actionError` or `uploadError`; no action throws.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useSettingsApi } from '@/composables/apis/useSettingsApi'
import { useUploadApi } from '@/composables/apis/useUploadApi'
import { errorCode } from '@/lib/errorHandler'
import { runAction } from '@/lib/runAction'
import type { ActionResult } from '@/lib/runAction'
import type { EmailTemplate, EmailTemplateContent } from '@/types/emailtemplate'
import type { MessageLanguage } from '@/locales/messages'
import type { CreateProductPayload, Product, ProductGroup, UpdateProductPayload } from '@/types/product'
import { ProductErrorCode } from '@/types/productenums'
import type { Setting } from '@/types/setting'
import type { BrandingImageKind } from '@/types/upload'

/** Holds the settings, email templates, products and groups the settings screens show. */
export const useSettingsStore = defineStore('settings', () => {
  const api = useSettingsApi()
  const { uploadBrandingImage } = useUploadApi()

  /** System settings list. */
  const settings = ref<Setting[]>([])

  /** Email templates list. */
  const emailTemplates = ref<EmailTemplate[]>([])

  /** Products list. */
  const products = ref<Product[]>([])

  /** Product groups list. */
  const productGroups = ref<ProductGroup[]>([])


  /** True while a list request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load or setting update, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed product create or update, null when none. */
  const actionError = ref<string | null>(null)

  /** The API's per-field messages for the last failed product create or update (`sku`, `metadata`, `gracePeriodDays` ...), empty when none. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Sentence for the last failed branding upload, null when none. */
  const uploadError = ref<string | null>(null)

  /**
   * Runs one list request under the loading flag and hands its result to the caller on success.
   *
   * @param task - The request to run.
   * @param assign - Receives the result when the request succeeded.
   * @returns Resolves when the request is over; a refusal is left in `error`.
   */
  const load = async <T>(task: () => Promise<T>, assign: (value: T) => void): Promise<void> => {
    loading.value = true
    const result = await runAction(error, task)
    loading.value = false
    if (result.ok) {
      assign(result.value)
    }
  }

  /**
   * Fetches system settings from the backend.
   *
   * @returns Resolves when the request is over.
   */
  const fetchSettings = (): Promise<void> =>
    load(api.listSettings, (rows) => {
      settings.value = rows
    })

  /**
   * Updates a single setting's value and refreshes the list.
   *
   * The backend exposes update-by-id only and no create, so a key that
   * was never seeded cannot be added from here. The banner shows the API's own sentence: a
   * refused value names what the key does accept, which a fixed "failed to save" would hide.
   *
   * @param id - Setting ID.
   * @param value - New value to store.
   * @returns True when persisted; false when refused (see `actionError`).
   */
  const updateSetting = async (id: number, value: string): Promise<boolean> => {
    const result = await runAction(actionError, () => api.updateSetting(id, value))
    if (result.ok) {
      await fetchSettings()
    }
    return result.ok
  }

  /**
   * Removes a secret setting's stored value and refreshes the list.
   *
   * @param id - Setting ID.
   * @returns True when cleared; false when refused (see `actionError`).
   */
  const clearSetting = async (id: number): Promise<boolean> => {
    const result = await runAction(actionError, () => api.clearSetting(id))
    if (result.ok) {
      await fetchSettings()
    }
    return result.ok
  }

  /**
   * Uploads a branding image and returns where it is served from.
   *
   * @param kind - Which branding slot the image is for.
   * @param file - The image file.
   * @returns The stored image's URL, or null when refused (see `uploadError`).
   */
  const uploadBrandingFile = async (kind: BrandingImageKind, file: File): Promise<string | null> => {
    const result = await runAction(uploadError, async () => (await uploadBrandingImage(kind, file)).url)
    return result.ok ? result.value : null
  }

  /**
   * Fetches email templates from the backend.
   *
   * @returns Resolves when the request is over.
   */
  const fetchEmailTemplates = (): Promise<void> =>
    load(api.listEmailTemplates, (rows) => {
      emailTemplates.value = rows
    })

  /**
   * Saves one language version of a template: rewrites the stored version, or adds it when the
   * template has none in that language yet, then refreshes the list.
   *
   * @param slug - Template slug.
   * @param language - The version's language.
   * @param existingId - Id of the stored version, null when there is none.
   * @param content - Subject, body and description.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const saveEmailTemplateVersion = async (
    slug: string,
    language: MessageLanguage,
    existingId: number | null,
    content: EmailTemplateContent,
  ): Promise<boolean> => {
    const result = await runAction(actionError, () =>
      existingId === null
        ? api.createEmailTemplate({ slug, language, ...content }).then(() => undefined)
        : api.updateEmailTemplate(existingId, content))
    if (result.ok) {
      await fetchEmailTemplates()
    }
    return result.ok
  }

  /**
   * Fetches all products from the backend (including inactive).
   *
   * @returns Resolves when the request is over.
   */
  const fetchProducts = (): Promise<void> =>
    load(api.listProducts, (rows) => {
      products.value = rows
    })

  /**
   * Fetches all product groups from the backend.
   *
   * @returns Resolves when the request is over.
   */
  const fetchProductGroups = (): Promise<void> =>
    load(api.listProductGroups, (rows) => {
      productGroups.value = rows
    })

  /**
   * Runs one product save under the field-error capture. A taken SKU answers 409 without a field
   * map, so its sentence is moved onto the `sku` field, where the operator is looking, instead
   * of the form-level message.
   *
   * @param task - The create or update request.
   * @returns The outcome of the request; a refusal is left in `actionError` and `fieldErrors`.
   */
  const saveProduct = async <T>(task: () => Promise<T>): Promise<ActionResult<T>> => {
    let skuTaken = false
    const result = await runAction(actionError, async () => {
      try {
        return await task()
      } catch (cause) {
        skuTaken = errorCode(cause) === ProductErrorCode.SkuAlreadyInUse
        throw cause
      }
    }, fieldErrors)
    if (!result.ok && skuTaken && actionError.value !== null) {
      fieldErrors.value = { ...fieldErrors.value, sku: actionError.value }
      actionError.value = null
    }
    return result
  }

  /**
   * Drops the API's message for one field of the product form, once the operator changed it.
   *
   * @param field - The wire name of the field (`sku`, `metadata`, `gracePeriodDays` ...).
   * @returns Nothing; the field's message is removed from the shared field errors.
   */
  const clearFieldError = (field: string): void => {
    const rest = { ...fieldErrors.value }
    delete rest[field]
    fieldErrors.value = rest
  }

  /**
   * Creates a new product and refreshes the list.
   *
   * @param payload - Product creation data.
   * @returns The new product ID, or null when refused (see `actionError` and `fieldErrors`).
   */
  const createProduct = async (payload: CreateProductPayload): Promise<number | null> => {
    const result = await saveProduct(() => api.createProduct(payload))
    if (!result.ok) {
      return null
    }
    await fetchProducts()
    return result.value
  }

  /**
   * Updates an existing product and refreshes the list.
   *
   * @param id - Product ID.
   * @param payload - Updated product data.
   * @returns True when updated; false when refused (see `actionError` and `fieldErrors`).
   */
  const updateProduct = async (id: number, payload: UpdateProductPayload): Promise<boolean> => {
    const result = await saveProduct(() => api.updateProduct(id, payload))
    if (result.ok) {
      await fetchProducts()
    }
    return result.ok
  }

  return {
    settings, emailTemplates, products, productGroups, fieldErrors, clearFieldError,
    loading, error, actionError, uploadError,
    fetchSettings, updateSetting, clearSetting, fetchEmailTemplates, saveEmailTemplateVersion, fetchProducts, fetchProductGroups,
    createProduct, updateProduct, uploadBrandingFile,
  }
})
