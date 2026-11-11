/** A plan a service can move to, as `GET /api/portal/client/services/{id}/plan-change-options` returns it. */
import type { ServiceChangeType } from '~/types/servicechangetype'

/** One plan a service can move to, with the prorated amount worked out before the client confirms. */
export interface ServicePlanChangeOption {
  /** The plan's product id. */
  productId: number
  /** The plan's display name. */
  name: string
  /** Whether the move is an upgrade or a downgrade. */
  type: ServiceChangeType
  /** The plan's price per billing cycle. */
  newRecurringAmount: number
  /** The prorated difference for the rest of the cycle, never negative: charged for an upgrade, credited for a downgrade. */
  prorationAmount: number
  /** ISO 4217 code of the client's billing currency. */
  currencyCode: string
}
