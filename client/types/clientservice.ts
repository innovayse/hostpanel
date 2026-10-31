/**
 * One hosting service the client owns, exactly as the backend's `ClientServiceDto` sends it
 * (camelCase wire). `GET /api/portal/client/services` returns a list of these and
 * `GET /api/portal/client/services/{id}` returns one.
 *
 * @module types/clientservice
 */

import type { BillingCycle } from '~/types/billingcycle'
import type { ServiceServer } from '~/types/serviceserver'
import type { ServiceStatus } from '~/types/servicestatus'
import type { ServiceUsage } from '~/types/serviceusage'
import type { SuspensionSource } from '~/types/suspensionsource'

/** One hosting service the client owns. */
export interface ClientService {
  /** Service primary key. */
  id: number
  /** Id of the product the service was ordered from. */
  productId: number
  /** Name of the ordered product; every service has one, since its product cannot be deleted. */
  productName: string
  /** SKU of the ordered product; null when the product has none. */
  sku: string | null
  /** True when the product is a digital one, delivered by a fulfilment plugin: there is nothing to set up on a server. */
  isDigital: boolean
  /** Billing cycle the service renews on. */
  billingCycle: BillingCycle
  /** Current lifecycle status. */
  status: ServiceStatus
  /** Next renewal date (ISO 8601); null while the service is not active yet. */
  nextRenewalAt: string | null
  /** Domain the service is attached to; null when none. */
  domain: string | null
  /** Control-panel username; null until the service is set up. */
  username: string | null
  /** Amount charged each cycle. */
  price: number
  /** Amount charged on the first invoice. */
  firstPaymentAmount: number
  /** Payment method used; null when not set. */
  paymentMethod: string | null
  /** The assigned server; null when none is assigned. */
  server: ServiceServer | null
  /** When the service was ordered (ISO 8601), shown as its registration date. */
  createdAt: string
  /** Why the service is suspended; null when it is not suspended. */
  suspensionSource: SuspensionSource | null
  /** The disk and bandwidth figures the server last reported; null when it reported none. */
  usage: ServiceUsage | null
}
