import type { DomainResult } from '~/templates/aurora/types'

/** Shape returned by GET /api/portal/public/tld-pricing. */
interface TldPricingResponse {
  currency: { code: string, prefix: string }
  pricing: Record<string, {
    register: Record<string, string>
    transfer?: Record<string, string>
    renew?: Record<string, string>
    categories?: string[]
  }>
}

/** Shape returned by POST /api/portal/public/domain-check — one domain per call. */
interface DomainCheckResponse {
  domain: string
  available: boolean
  status: string
}

/** One row of the TLD price table. */
export interface TldPriceRow {
  /** Extension with its leading dot, e.g. ".am". */
  tld: string
  /** Formatted one-year registration price, or a dash. */
  register: string
  /** Formatted one-year renewal price, or a dash. */
  renew: string
  /** Formatted one-year transfer price, or a dash. */
  transfer: string
  /** Category tags the backend assigns, used for filtering. */
  categories: string[]
}

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
  const { data } = useFetch<TldPricingResponse>('/api/portal/public/tld-pricing')

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
    })))

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
            const response = await $fetch<DomainCheckResponse>('/api/portal/public/domain-check', {
              method: 'POST',
              body: { domain: name },
            })
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

  return { priceRows, offeredTlds, results, pending, search }
}
