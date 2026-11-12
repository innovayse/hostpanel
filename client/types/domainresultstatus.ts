/**
 * Availability of one checked domain name.
 *
 * @module types/domainresultstatus
 */

/** Availability of one checked domain name. */
export enum DomainResultStatus {
  /** The registrar reports the name as free. */
  Available = 'available',
  /** The name is registered to someone else. */
  Taken = 'taken',
  /** The check did not complete. */
  Unknown = 'unknown',
}
