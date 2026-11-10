/**
 * Rules the order screens share about one order line: which locale key words the code its last
 * attempt failed with.
 *
 * Whether staff may retry a line or close it by hand is not worked out here: the API tells it on
 * each line (`canRetry`, `canClose`), with the retry lease it is configured with.
 */

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
  ORDER_LINE_INTERRUPTED: 'orders.lineErrors.orderLineInterrupted',
  ORDER_LINE_SERVICE_CONFLICT: 'orders.lineErrors.orderLineServiceConflict',
}

/** Locale key of the sentence for a code this panel has no wording for. */
const GENERIC_LINE_ERROR_KEY = 'orders.lineErrors.generic'

/**
 * Picks the locale key of the sentence that explains a fulfilment error code.
 *
 * @param code - The code the last failed attempt ended with.
 * @returns The key of the known sentence, or the generic one for an unknown code.
 */
export const orderLineErrorKey = (code: string): string => LINE_ERROR_KEYS[code] ?? GENERIC_LINE_ERROR_KEY
