/**
 * Endpoints behind the settings store: system settings, email templates, products, product
 * and groups.
 *
 * No state lives here; the settings store owns it. Every function rejects with an `ApiError`
 * carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { CreateEmailTemplatePayload, EmailTemplate, EmailTemplateContent } from '@/types/emailtemplate'
import type { PaginatedList } from '@/types/paginatedlist'
import type {
  CreateProductPayload, PaymentMethodOption, Product, ProductGroup, ProductGroupPayload, ProductListQuery, UpdateProductPayload,
} from '@/types/product'
import type { Setting } from '@/types/setting'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for settings, email templates and products.
 *
 * @returns One typed function per endpoint.
 */
export const useSettingsApi = () => {
  const { request } = useApi()

  /**
   * Lists the system settings.
   *
   * @returns Every setting row.
   */
  const listSettings = (): Promise<Setting[]> => request<Setting[]>(ApiPath.AdminSettings)

  /**
   * Writes one setting's value; the API updates by id and never creates.
   *
   * @param id - Setting id.
   * @param value - New value.
   * @returns Nothing; the API answers 204.
   */
  const updateSetting = (id: number, value: string): Promise<void> =>
    request<void>(`${ApiPath.AdminSettings}/${id}`, { method: HttpMethod.Put, body: JSON.stringify({ value }) })

  /**
   * Lists the email templates.
   *
   * @returns Every language version of every template.
   */
  const listEmailTemplates = (): Promise<EmailTemplate[]> =>
    request<EmailTemplate[]>(ApiPath.AdminEmailTemplates)

  /**
   * Adds a language version to a template.
   *
   * @param payload - Slug, language and words of the new version.
   * @returns The new version's id.
   */
  const createEmailTemplate = (payload: CreateEmailTemplatePayload): Promise<number> =>
    request<number>(ApiPath.AdminEmailTemplates, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Rewrites the words of one stored language version.
   *
   * @param id - Version id.
   * @param content - New subject, body and description.
   * @returns Nothing; the API answers 204.
   */
  const updateEmailTemplate = (id: number, content: EmailTemplateContent): Promise<void> =>
    request<void>(`${ApiPath.AdminEmailTemplates}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(content) })

  /**
   * Lists every product, inactive and unsellable ones included.
   *
   * @returns Every product.
   */
  const listProducts = (): Promise<Product[]> =>
    request<Product[]>(`${ApiPath.Products}${buildQuery({ activeOnly: false, includeUnsellable: true })}`)

  /**
   * Lists one page of the products, inactive and unsellable ones included.
   *
   * @param query - Page, page size, search text, group, type and status.
   * @returns The page and the total count.
   */
  const listProductsPage = (query: ProductListQuery): Promise<PaginatedList<Product>> =>
    request<PaginatedList<Product>>(
      `${ApiPath.AdminProducts}${buildQuery({
        page: query.page,
        pageSize: query.pageSize,
        search: query.search,
        groupId: query.groupId,
        type: query.type,
        status: query.status,
      })}`,
    )

  /**
   * Lists every product group, inactive ones included.
   *
   * @returns Every group.
   */
  const listProductGroups = (): Promise<ProductGroup[]> =>
    request<ProductGroup[]>(`${ApiPath.Products}/groups${buildQuery({ activeOnly: false })}`)

  /**
   * Lists the payment methods a checkout offers right now, for the group form's choices.
   *
   * @returns The methods on offer.
   */
  const listPaymentMethods = (): Promise<PaymentMethodOption[]> =>
    request<PaymentMethodOption[]>(ApiPath.PaymentMethods)

  /**
   * Creates a product group.
   *
   * @param payload - Group data.
   * @returns The new group id.
   */
  const createProductGroup = (payload: ProductGroupPayload): Promise<number> =>
    request<number>(`${ApiPath.Products}/groups`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a product group; its name, description and allowed payment methods are replaced.
   *
   * @param id - Group id.
   * @param payload - Updated group data.
   * @returns Nothing; the API answers 204.
   */
  const updateProductGroup = (id: number, payload: ProductGroupPayload): Promise<void> =>
    request<void>(`${ApiPath.Products}/groups/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Creates a product.
   *
   * @param payload - Product data.
   * @returns The new product id.
   */
  const createProduct = (payload: CreateProductPayload): Promise<number> =>
    request<number>(ApiPath.Products, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a product; the id travels in the body as well as the path.
   *
   * @param id - Product id.
   * @param payload - Updated product data.
   * @returns Nothing; the API answers 204.
   */
  const updateProduct = (id: number, payload: UpdateProductPayload): Promise<void> =>
    request<void>(`${ApiPath.Products}/${id}`, { method: HttpMethod.Put, body: JSON.stringify({ id, ...payload }) })

  return {
    listSettings,
    updateSetting,
    listEmailTemplates,
    createEmailTemplate,
    updateEmailTemplate,
    listProducts,
    listProductsPage,
    listProductGroups,
    listPaymentMethods,
    createProductGroup,
    updateProductGroup,
    createProduct,
    updateProduct,
  }
}
