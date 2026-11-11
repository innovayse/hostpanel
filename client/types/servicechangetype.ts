/**
 * Whether a plan change moves a service up or down.
 *
 * These are **wire values**: each string is the exact member name of the backend
 * `ServiceChangeType` enum. The sentence a person reads comes from the locale files.
 */
export enum ServiceChangeType {
  /** A dearer plan; the prorated difference is charged on an invoice. */
  Upgrade = 'Upgrade',
  /** A cheaper plan; the prorated difference is credited to the balance. */
  Downgrade = 'Downgrade'
}
