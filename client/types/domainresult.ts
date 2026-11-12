/**
 * One row of the aurora domain availability list.
 *
 * @module types/domainresult
 */

import type { DomainResultStatus } from '~/types/domainresultstatus'

/** One row of the domain availability list rendered by the aurora domain search. */
export interface DomainResult {
  /** Fully qualified domain, e.g. "example.com". */
  name: string
  /** Formatted price, or a dash when unavailable or unpriced. */
  price: string
  /**
   * Availability as reported by the registrar check.
   *
   * `unknown` is its own state rather than being folded into `taken`: a check
   * that failed (no registrar configured, registrar unreachable) is not the
   * same as a name someone owns, and showing it as taken hides the fault.
   */
  status: `${DomainResultStatus}`
}
