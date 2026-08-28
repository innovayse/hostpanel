import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { ALL_CATEGORY } from '~/templates/aurora/types'
import type { DomainResult } from '~/templates/aurora/types'
import type { TldPriceRow } from '~/types/tldpricerow'

/**
 * Domain pricing and availability lookups.
 *
 * The backend checks one domain per request, so a search fans out across the
 * offered extensions and waits for all of them.
 *
 * @param tldLimit How many extensions the search offers.
 * @returns Price rows, the search results, pending state and the search action.
 */
export const useDomainLookup = (tldLimit = 4) => {
  // Through the API composable rather than a raw `useFetch`: that is the layer that owns the
  // URL, and `useApi()` beneath it sends the locale header the raw call was skipping.
  const { loadTldPricing, checkDomain } = useCatalogApi()
  const { data } = loadTldPricing()

  const results = ref<DomainResult[]>([])
  const pending = ref(false)

  const prefix = computed(() => data.value?.currency?.prefix ?? '')

  /**
   * Formats a price from the period-keyed map, falling back to a dash.
   *
   * @param map Period-keyed price map as the API returned it.
   */
  const oneYear = (map: Record<string, string> | undefined) => {
    const value = map?.['1']
    return value ? `${prefix.value}${value}` : '—'
  }

  /** Every priced extension, for the full price table. */
  const priceRows = computed<TldPriceRow[]>(() =>
    Object.entries(data.value?.pricing ?? {}).map(([tld, entry]) => ({
      tld: tld.startsWith('.') ? tld : `.${tld}`,
      register: oneYear(entry?.register),
      renew: oneYear(entry?.renew ?? entry?.register),
      transfer: oneYear(entry?.transfer ?? entry?.register),
      categories: entry?.categories ?? [],
      registerAmount: Number(entry?.register?.['1'] ?? 0) || 0,
    })))

  /**
   * Every category the priced zones carry, prefixed with the "all" sentinel.
   * Derived from the registrar data rather than a fixed list, so an operator's
   * own categories drive the filter.
   */
  const categories = computed(() => {
    const found = new Set<string>()
    for (const row of priceRows.value) for (const category of row.categories) found.add(category)
    return [ALL_CATEGORY, ...[...found].sort()]
  })

  /** The subset the search card offers. */
  const offeredTlds = computed(() => priceRows.value.slice(0, tldLimit))

  /**
   * Checks a name against every offered extension.
   *
   * A check that fails is reported as `unknown` rather than dropped or shown as
   * taken: the row count stays stable, and a registrar that is unconfigured or
   * unreachable reads as a fault instead of as a name somebody owns.
   *
   * @param term Raw text the visitor typed.
   */
  const search = async (term: string) => {
    const base = term.trim().toLowerCase().replace(/\s+/g, '-').replace(/\.[^.]*$/, '')
    if (!base) return

    pending.value = true
    try {
      results.value = await Promise.all(
        offeredTlds.value.map(async ({ tld, register }) => {
          const name = `${base}${tld}`
          try {
            const response = await checkDomain(name)
            return {
              name,
              price: response.available ? register : '—',
              status: response.available ? 'available' : 'taken',
            } satisfies DomainResult
          } catch {
            return { name, price: '—', status: 'unknown' } satisfies DomainResult
          }
        })
      )
    } finally {
      pending.value = false
    }
  }

  /**
   * Currency the prices are quoted in, as the API reports it. The cart converts
   * from AMD when told to, so a caller has to know which it is holding rather
   * than assuming, the way the original page did.
   */
  const currencyCode = computed(() => data.value?.currency?.code ?? '')

  return { priceRows, categories, currencyCode, offeredTlds, results, pending, search }
}
