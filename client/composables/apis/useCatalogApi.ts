/**
 * Endpoints under `/api/portal/public`: the catalogue and the reference data the storefront
 * renders before anybody signs in.
 *
 * Every function returns a plain `Promise` through `apiFetch`, which is what lets
 * `stores/catalog.ts` call them from a store action, including the action a server-rendered
 * page wraps in `useAsyncData`. Nothing here is cached or held: the store keeps the state.
 *
 * @module composables/apis/useCatalogApi
 */

import { apiFetch } from '~/composables/useApi'
import type { CatalogBundle } from '~/types/catalogbundle'
import type { CountryOption } from '~/types/countryoption'
import type { DomainCheckResult } from '~/types/domaincheckresult'
import type { PortalCurrency } from '~/types/portalcurrency'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import type { PortalProductQuery } from '~/types/portalproductquery'
import type { ProductFeature } from '~/types/productfeature'
import type { SlidePublic } from '~/types/slidepublic'
import type { TldPricing } from '~/types/tldpricing'
import { HttpMethod } from '~/types/httpmethod'

/**
 * The public catalogue surface, one function per endpoint.
 *
 * @returns The catalogue endpoint functions.
 */
export const useCatalogApi = () => {
  /**
   * The Nuxt app this surface was created in. A store action that awaits one read and then starts
   * the next runs the second read outside the render, where `useNuxtApp()` and `useRequestFetch()`
   * throw NUXT_E1001 on the server; the read then failed without ever reaching the backend and the
   * home page showed one family card. Each read is therefore run inside this app's context.
   */
  const nuxtApp = useNuxtApp()

  /**
   * Runs one `apiFetch` inside the captured Nuxt context.
   *
   * @param url - Endpoint URL.
   * @param opts - `$fetch` options.
   * @returns The parsed reply.
   * @throws Whatever `apiFetch` throws.
   */
  const read = <T>(url: string, opts?: Parameters<typeof apiFetch>[1]): Promise<T> =>
    nuxtApp.runWithContext(() => apiFetch<T>(url, opts))

  /**
   * Reads the product catalogue.
   *
   * @param query - Filters to apply. Omit for the whole catalogue.
   * @returns The matching products.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchProducts = <T = PortalProduct>(query?: PortalProductQuery): Promise<T[]> =>
    read<T[]>('/api/portal/public/products', { query })

  /**
   * Reads the product groups the storefront shows as product families.
   *
   * @returns The groups.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchProductGroups = (): Promise<PortalProductGroup[]> =>
    read<PortalProductGroup[]>('/api/portal/public/product-groups')

  /**
   * Reads the active product bundles the storefront offers.
   *
   * @returns The bundles.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchProductBundles = (): Promise<CatalogBundle[]> =>
    read<CatalogBundle[]>('/api/portal/public/product-bundles')

  /**
   * Reads the specification lines behind the plan comparison table.
   *
   * @param groupId - Product group whose plans are being compared.
   * @returns The specification lines.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchProductFeatures = (groupId: number): Promise<ProductFeature[]> =>
    read<ProductFeature[]>('/api/portal/public/product-features', { query: { gid: groupId } })

  /**
   * Reads the billing currencies.
   *
   * @returns The currency list.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchCurrencies = (): Promise<PortalCurrency[]> =>
    read<PortalCurrency[]>('/api/portal/public/currencies')

  /**
   * Reads the TLD price table.
   *
   * @param currency - ISO 4217 code to quote prices in: the payer's currency, never the page's
   * language. Omit to take the backend's default.
   * @returns The price table.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchTldPricing = (currency?: string): Promise<TldPricing> =>
    read<TldPricing>('/api/portal/public/tld-pricing', { query: currency ? { currency } : {} })

  /**
   * Reads the marketing slides on the home page.
   *
   * @param lang - Locale to return slide copy in.
   * @returns The slides.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchSlides = (lang: string): Promise<SlidePublic[]> =>
    read<SlidePublic[]>('/api/portal/public/slides', { query: { lang } })

  /**
   * Asks whether one domain is available.
   *
   * One domain per request is the backend's contract, so a search over several extensions is
   * several calls; fanning them out is the caller's business, not this layer's.
   *
   * @param domain - Fully qualified domain name to check.
   * @returns Availability and the registrar's own status word.
   * @throws Whatever `apiFetch` throws: a registrar that is unreachable included.
   */
  const checkDomain = (domain: string): Promise<DomainCheckResult> =>
    read<DomainCheckResult>('/api/portal/public/domain-check', {
      method: HttpMethod.Post,
      body: { domain }
    })

  /**
   * Reads the country list the address forms offer.
   *
   * @returns The country options.
   * @throws Whatever `apiFetch` throws.
   */
  const fetchCountries = (): Promise<CountryOption[]> =>
    read<CountryOption[]>('/api/portal/public/countries')

  return {
    fetchProducts,
    fetchProductGroups,
    fetchProductBundles,
    fetchProductFeatures,
    fetchCurrencies,
    fetchCountries,
    fetchTldPricing,
    fetchSlides,
    checkDomain
  }
}
