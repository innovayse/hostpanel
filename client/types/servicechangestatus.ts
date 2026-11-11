/**
 * Where a plan change request stands, as the backend names it (`ServiceChangeStatus` member names).
 */
export enum ServiceChangeStatus {
  /** A downgrade waiting for staff. */
  Pending = 'Pending',
  /** An upgrade waiting for its invoice to be paid. */
  Approved = 'Approved',
  /** The service already runs on the new plan. */
  Completed = 'Completed',
  /** Staff refused the change. */
  Rejected = 'Rejected'
}
