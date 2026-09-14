import { useCatalogApi } from '~/composables/apis/useCatalogApi'
import { useCurrencyStore } from '~/stores/currency'
import { formatMoney } from '~/utils/formatMoney'
import { parseDescription } from '~/utils/whmcs'
import type { PlanCard } from '~/templates/aurora/types'

/**
 * Product group the storefront's plan cards draw from — shared hosting.
 *
 * Without it the endpoint returns the whole catalogue, so the hosting page listed
 * SSL certificates, mailboxes and domain registration beside the hosting plans.
 * `classic`'s page always passed this; the first version of this composable did
 * not, and a stub returning only hosting products hid the difference. The real
 * backend showed it immediately.
 */
export const HOSTING_GROUP_ID = 1

/**
 * Loads hosting products and maps them to the shape the plan cards render.
 *
 * Lives in a composable rather than in a page because two pages need it, and in a composable
 * rather than a template because templates never fetch. Prices are formatted here, in the
 * payer's own currency — `stores/currency.ts` — never the page's language; see
 * `docs/superpowers/specs/2026-09-13-multi-currency-pricing-design.md` §5.
 *
 * @returns The mapped plans and the underlying request's pending state.
 */
export const usePortalPlans = () => {
  const localePath = useLocalePath()
  const currencyStore = useCurrencyStore()

  // Through the API composable rather than a raw `useFetch`: that is the layer that owns the
  // URL, and `useApi()` beneath it sends the locale header the raw call was skipping.
  // `currency` is passed the same way `useDomainLookup.ts` passes it to `loadTldPricing` — the
  // payer's currency, not the page's language — and the getter re-reads once it resolves so the
  // request (and its `pricing`) re-fetches when `payerCode` changes.
  const { data, pending } = useCatalogApi().loadProducts(
    () => ({ gid: HOSTING_GROUP_ID, currency: currencyStore.payerCode ?? undefined })
  )

  const plans = computed<PlanCard[]>(() => (data.value ?? []).map((product) => {
    const currency = currencyStore.moneyCurrencyFor(currencyStore.payerCode)

    /**
     * Formats an amount in the payer's currency, or a dash when the backend reports no price.
     *
     * @param raw Amount as the API returned it, or null when not offered.
     * @param divisor Divide before formatting — 12 turns an annual price into a monthly one.
     */
    const format = (raw: number | null | undefined, divisor = 1) => {
      if (raw === null || raw === undefined) return '—'
      return formatMoney(raw / divisor, currency)
    }

    // A description is authored either as plain text or as HTML with <br /> between
    // items — WHMCS lets the same field be edited both ways, and both are in the
    // catalogue. Printed straight into the card, the HTML variant showed its own
    // markup: "✔ 20 GB Disk Space <br /> ✔ 200 GB Bandwidth". parseDescription
    // already understands both, and separates the summary from the feature lines
    // the card can then render as a list.
    const { summary, features } = parseDescription(product.description ?? '')

    // Free is decided by the price, not by the name. Matching on "free" in a name —
    // which the classic page does — features a "Freelancer Hosting" that costs money
    // and misses a free plan called anything else, in any language. The number is the
    // thing being advertised.
    const monthly = product.pricing?.monthly
    const isFree = monthly === 0

    return {
      id: product.id,
      name: product.name,
      description: summary,
      features,
      isFree,
      priceMonthly: format(monthly),
      priceAnnual: format(product.pricing?.annual, 12),
      href: localePath(`/configure/${product.id}`),
    }
  }))

  return { plans, pending }
}
