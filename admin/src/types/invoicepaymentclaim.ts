/**
 * One invoice payment claim (a stored payment method charge the host started), the statuses a claim
 * can be in, and the filter the claims are listed by.
 */

/** Where a claim stands; the values are the names the API sends. */
export enum InvoicePaymentClaimStatus {
  Open = 'Open',
  ActionRequired = 'ActionRequired',
  Pending = 'Pending',
  Settled = 'Settled',
  Declined = 'Declined',
  Abandoned = 'Abandoned',
  Held = 'Held',
}

/** A single claim as the claims list returns it. */
export interface InvoicePaymentClaim {
  /** Unique claim key. */
  id: number
  /** The invoice the charge is for. */
  invoiceId: number
  /** The client whose method was charged. */
  clientId: number
  /** Full name of the client. */
  clientName: string
  /** Id of the payment plugin that was asked to charge. */
  gatewayModuleId: string
  /** Amount asked of the gateway, in whole units of `currency`. */
  amount: number
  /** ISO 4217 code of the amount. */
  currency: string
  /** Where the charge stands. */
  status: InvoicePaymentClaimStatus
  /** The gateway's transaction reference once it took money, otherwise null. */
  transactionId: string | null
  /** ISO 8601 time the claim was written. */
  createdAt: string
  /** ISO 8601 time the claim reached a final status, null while unresolved. */
  resolvedAt: string | null
}
