/**
 * Endpoints under `/api/portal/public` — the catalogue and the reference data the storefront
 * renders before anybody signs in.
 *
 * Two naming shapes, as elsewhere in `composables/apis/`: `load*` returns the `useApi()`
 * handle for a server-rendered read, `fetch*` returns a plain `Promise` for a one-shot call.
 * Every page reading this area is public and has to be indexable, so most of it is `load*`.
 * Nothing here is cached or held — `useApi()`'s own key-based dedup is the only caching.
 *
 * @module composables/apis/useCatalogApi
 */

import { apiFetch, useApi } from '~/composables/useApi'
import type { CountryOption } from '~/types/countryoption'
import type { DomainCheckResult } from '~/types/domaincheckresult'
import type { PortalCurrency } from '~/types/portalcurrency'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductQuery } from '~/types/portalproductquery'
import type { ProductFeature } from '~/types/productfeature'
import type { TldPricing } from '~/types/tldpricing'

/**
 * The public catalogue surface, one function per endpoint.
 *
 * @returns The catalogue endpoint functions.
 */
export function useCatalogApi() {
  /**
   * Server-rendered read of the product catalogue.
   *
   * @param query - Reactive filters; re-reads whenever they change, as `useApi()` watches them.
   * @returns The `useApi()` handle for the product list, defaulting to an empty list so a
   * page never has to guard against `null` before the first response arrives.
   */
  const loadProducts = (query: () => PortalProductQuery) =>
    useApi<PortalProduct[]>('/api/portal/public/products', {
      query: () => query() as Record<string, unknown>,
      default: () => []
    })

  /**
   * One-shot read of the product catalogue, for an event handler rather than a page load.
   *
   * @param query - Filters to apply. Omit for the whole catalogue.
   * @returns The matching products.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchProducts = <T>(query?: PortalProductQuery): Promise<T[]> =>
    apiFetch<T[]>('/api/portal/public/products', { query })

  /**
   * Server-rendered read of the specification lines behind the plan comparison table.
   *
   * @param groupId - Product group whose plans are being compared.
   * @returns The `useApi()` handle for the specification lines.
   */
  const loadProductFeatures = (groupId: number) =>
    useApi<ProductFeature[]>('/api/portal/public/product-features', {
      query: () => ({ gid: groupId }),
      default: () => []
    })

  /**
   * Read of the billing currencies.
   *
   * @param server - False to skip this during SSR. The currency list only decorates amounts
   * that are themselves client-side, so the storefront does not pay for it on every render.
   * @returns The `useApi()` handle for the currency list.
   */
  const loadCurrencies = (server = true) =>
    useApi<PortalCurrency[]>('/api/portal/public/currencies', { server, default: () => [] })

  /**
   * Server-rendered read of the TLD price table.
   *
   * @param currency - ISO 4217 code to quote prices in. Omit to take the backend's default.
   * A getter so the table re-reads when the visitor switches locale.
   * @returns The `useApi()` handle for the price table.
   */
  const loadTldPricing = (currency?: () => string) =>
    useApi<TldPricing>('/api/portal/public/tld-pricing', {
      query: () => (currency ? { currency: currency() } : {})
    })

  /**
   * Server-rendered read of the marketing slides on the home page.
   *
   * @param lang - Locale to return slide copy in; a getter so it re-reads on a locale switch.
   * @returns The `useApi()` handle for the slides, defaulting to an empty list.
   */
  const loadSlides = <T>(lang: () => string) =>
    useApi<T[]>('/api/portal/public/slides', {
      query: () => ({ lang: lang() }),
      default: () => []
    })

  /**
   * Asks whether one domain is available.
   *
   * One domain per request is the backend's contract, so a search over several extensions is
   * several calls; fanning them out is the caller's business, not this layer's.
   *
   * @param domain - Fully qualified domain name to check.
   * @returns Availability and the registrar's own status word.
   * @throws Whatever `apiFetch` throws — a registrar that is unreachable included.
   */
  const checkDomain = (domain: string): Promise<DomainCheckResult> =>
    apiFetch<DomainCheckResult>('/api/portal/public/domain-check', {
      method: 'POST',
      body: { domain }
    })

  /**
   * Server-rendered read of the country list the address forms offer.
   *
   * @returns The `useApi()` handle for the country options, defaulting to an empty list.
   */
  const loadCountries = () =>
    useApi<CountryOption[]>('/api/portal/public/countries', { default: () => [] })

  return {
    loadProducts,
    fetchProducts,
    loadProductFeatures,
    loadCurrencies,
    loadCountries,
    loadTldPricing,
    loadSlides,
    checkDomain
  }
}
