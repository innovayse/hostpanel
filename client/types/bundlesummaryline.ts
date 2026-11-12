/**
 * One priced line of a bundle's order summary.
 *
 * @module types/bundlesummaryline
 */

/** One line of the bundle summary: a component or the domain, with its estimated price. */
export interface BundleSummaryLine {
  /** Stable key of the line. */
  id: string
  /** The product or domain name. */
  name: string
  /** The cycle or period the price covers, e.g. "Annually" or "2 Years". */
  detail: string
  /** The line's estimated price, formatted. */
  price: string
}
