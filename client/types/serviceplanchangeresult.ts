/** What a plan change request came to, as `POST /api/portal/client/services/{id}/plan-change` returns it. */
import type { ServiceChangeStatus } from '~/types/servicechangestatus'
import type { ServiceChangeType } from '~/types/servicechangetype'

/** The outcome of a plan change request. */
export interface ServicePlanChangeResult {
  /** The plan change request. */
  requestId: number
  /** Whether the move is an upgrade or a downgrade. */
  type: ServiceChangeType
  /** Where the request stands. */
  status: ServiceChangeStatus
  /** The prorated difference. */
  prorationAmount: number
  /** ISO 4217 code of the client's billing currency. */
  currencyCode: string
  /** The proration invoice the client pays for an upgrade, or null when there is none. */
  invoiceId: number | null
}
