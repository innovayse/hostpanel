/**
 * One choice of a select domain field.
 *
 * @module types/domainfieldoption
 */

/** One choice of a select domain field, as `GET /api/portal/public/domain-fields` answers it. */
export interface DomainFieldOption {
  /** The value stored and sent when the choice is picked. */
  value: string
  /** The text shown for the choice, already in the reader's language. */
  label: string
}
