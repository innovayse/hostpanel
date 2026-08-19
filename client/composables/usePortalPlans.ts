import { parseDescription } from '~/utils/whmcs'
import type { PlanCard } from '~/templates/aurora/types'

/**
 * Shape returned by GET /api/portal/public/products. The proxy adds `pid` and a
 * WHMCS-compatible `pricing.USD` block on top of the backend's ProductDto.
 */
interface ProductResponse {
  id: number
  name: string
  description?: string | null
  slug?: string | null
  pricing?: { USD?: { prefix?: string, suffix?: string, monthly?: string, annually?: string } }
}

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
 * Lives in a composable rather than in a page because two pages need it, and in
 * a composable rather than a template because templates never fetch. Prices are
 * formatted here: the currency prefix comes from the API, so the formatting
 * cannot live in markup that has no access to it.
 *
 * @returns The mapped plans and the underlying request's pending state.
 */
export const usePortalPlans = () => {
  const localePath = useLocalePath()
  const { locale } = useI18n()

  const { data, pending } = useFetch<ProductResponse[]>('/api/portal/public/products', {
    query: computed(() => ({ lang: locale.value, gid: HOSTING_GROUP_ID })),
  })

  const plans = computed<PlanCard[]>(() => (data.value ?? []).map((product) => {
    const money = product.pricing?.USD
    const prefix = money?.prefix ?? ''
    const suffix = money?.suffix ?? ''

    /**
     * Formats an amount, or a dash when the backend reports no price.
     *
     * @param raw Amount as the API returned it.
     * @param divisor Divide before formatting — 12 turns an annual price into a monthly one.
     */
    const format = (raw: string | undefined, divisor = 1) => {
      const value = Number(raw)
      if (!raw || Number.isNaN(value) || value < 0) return '—'
      return `${prefix}${(value / divisor).toFixed(2)}${suffix}`
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
    const monthly = Number(money?.monthly)
    const isFree = Number.isFinite(monthly) && monthly === 0

    return {
      id: product.id,
      name: product.name,
      description: summary,
      features,
      isFree,
      priceMonthly: format(money?.monthly),
      priceAnnual: format(money?.annually, 12),
      href: localePath(`/configure/${product.id}`),
    }
  }))

  return { plans, pending }
}
