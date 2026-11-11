/**
 * Plan change requests of client services as the admin API sends them
 * (`Innovayse.Hostpanel.Application.Services.Common.ServiceChangeRequestDto`): the member names verbatim.
 */

/** Whether a plan change moves a service to a dearer or a cheaper plan. */
export enum ServiceChangeType {
  /** The new plan costs more per cycle; the client pays the prorated difference. */
  Upgrade = 'Upgrade',
  /** The new plan costs less per cycle; the prorated difference goes to the client's balance. */
  Downgrade = 'Downgrade',
}

/** Where a plan change request stands. */
export enum ServiceChangeStatus {
  /** Waits for staff to approve or reject it. */
  Pending = 'Pending',
  /** Approved; applied once its invoice is paid and the hosting panel accepts it. */
  Approved = 'Approved',
  /** The service now runs on the new plan. */
  Completed = 'Completed',
  /** Turned down; the service stays on its plan. */
  Rejected = 'Rejected',
}

/** One plan change request in the admin list. */
export interface ServiceChangeRequestItem {
  /** Primary key of the request. */
  id: number
  /** The service whose plan changes. */
  serviceId: number
  /** The client that owns the service. */
  clientId: number
  /** Full name of that client. */
  clientName: string
  /** Product the service ran on when the request was made. */
  fromProductId: number
  /** Display name of that product. */
  fromProductName: string
  /** Product the service moves to. */
  toProductId: number
  /** Display name of that product. */
  toProductName: string
  /** Upgrade or downgrade. */
  type: ServiceChangeType
  /** Where the request stands. */
  status: ServiceChangeStatus
  /** Prorated charge of an upgrade or credit of a downgrade, in `currencyCode`. */
  prorationAmount: number
  /** ISO 4217 code of the client's billing currency. */
  currencyCode: string
  /** The new plan's recurring price per cycle, in `currencyCode`. */
  newRecurringAmount: number
  /** Proration invoice of an upgrade, null when there is none. */
  invoiceId: number | null
  /** Staff note given on approval or rejection, null when none. */
  note: string | null
  /** UTC timestamp when the request was made. */
  createdAt: string
  /** UTC timestamp when it was completed or rejected, null while open. */
  resolvedAt: string | null
}

/** What staff decide about a plan change request. */
export enum ServiceChangeDecision {
  /** Approve the request and apply it. */
  Approve = 'Approve',
  /** Turn the request down. */
  Reject = 'Reject',
}
