/**
 * Rules the order screens share about one order line: whether staff may retry it and which
 * locale key words the code its last attempt failed with.
 *
 * The retry rule mirrors the API: a failed line may be retried, and so may a line a retry claimed
 * so long ago that the run is taken to have died before recording its outcome.
 */
import { ORDER_LINE_RETRY_LEASE_MS } from '@/lib/limits'
import { OrderItemFulfilmentState, OrderStatus } from '@/types/order'
import type { OrderItemDetail } from '@/types/order'

/** Locale key of each known fulfilment error code; a code missing here reads as the generic sentence. */
const LINE_ERROR_KEYS: Record<string, string> = {
  DOMAIN_ALREADY_REGISTERED: 'orders.lineErrors.domainAlreadyRegistered',
  DOMAIN_REGISTRATION_UPSTREAM_FAILED: 'orders.lineErrors.domainRegistrationUpstreamFailed',
  DOMAIN_RENEWAL_UPSTREAM_FAILED: 'orders.lineErrors.domainRenewalUpstreamFailed',
  DOMAIN_TRANSFER_UPSTREAM_FAILED: 'orders.lineErrors.domainTransferUpstreamFailed',
  DOMAIN_PRODUCT_NOT_CONFIGURED: 'orders.lineErrors.domainProductNotConfigured',
  PRODUCT_NOT_FOUND: 'orders.lineErrors.productNotFound',
  PRODUCT_NOT_AVAILABLE: 'orders.lineErrors.productNotAvailable',
  PROVISIONING_UPSTREAM_FAILED: 'orders.lineErrors.provisioningUpstreamFailed',
  PROVISIONING_OUTCOME_UNKNOWN: 'orders.lineErrors.provisioningOutcomeUnknown',
  PROVISIONING_ACCOUNT_CONFLICT: 'orders.lineErrors.provisioningAccountConflict',
  PRODUCT_PACKAGE_MISSING: 'orders.lineErrors.productPackageMissing',
  PRODUCT_SERVER_GROUP_REQUIRED: 'orders.lineErrors.productServerGroupRequired',
  SERVER_CONNECTION_UNREACHABLE: 'orders.lineErrors.serverConnectionUnreachable',
  HOSTPANEL_UNEXPECTED: 'orders.lineErrors.unexpected',
}

/** Locale key of the sentence for a code this panel has no wording for. */
const GENERIC_LINE_ERROR_KEY = 'orders.lineErrors.generic'

/**
 * Tells whether staff may retry an order line now.
 *
 * @param orderStatus - Status of the order the line belongs to; only an incomplete order is retried.
 * @param item - The order line.
 * @param now - The current moment in epoch milliseconds, injectable for tests.
 * @returns True for a failed line, and for an in-progress line whose last attempt is older than the retry lease.
 */
export const canRetryOrderLine = (orderStatus: OrderStatus, item: OrderItemDetail, now: number = Date.now()): boolean => {
  if (orderStatus !== OrderStatus.Incomplete) {
    return false
  }
  if (item.fulfilmentState === OrderItemFulfilmentState.Failed) {
    return true
  }
  if (item.fulfilmentState !== OrderItemFulfilmentState.InProgress || item.lastFulfilmentAttemptAt === null) {
    return false
  }
  return Date.parse(item.lastFulfilmentAttemptAt) + ORDER_LINE_RETRY_LEASE_MS <= now
}

/**
 * Picks the locale key of the sentence that explains a fulfilment error code.
 *
 * @param code - The code the last failed attempt ended with.
 * @returns The key of the known sentence, or the generic one for an unknown code.
 */
export const orderLineErrorKey = (code: string): string => LINE_ERROR_KEYS[code] ?? GENERIC_LINE_ERROR_KEY
