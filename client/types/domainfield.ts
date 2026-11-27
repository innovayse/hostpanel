/**
 * One extra value a registry asks for beside the standard contact.
 *
 * @module types/domainfield
 */

import type { DomainFieldOption } from '~/types/domainfieldoption'
import type { DomainFieldType } from '~/types/domainfieldtype'

/**
 * One extra field a registrar plugin declares for a domain, as `GET /api/portal/public/domain-fields`
 * answers it. Every text is already in the reader's language; the screen renders the field and never
 * interprets what it means.
 */
export interface DomainField {
  /** The key the value is sent and stored under. */
  key: string
  /** The text shown for the field. */
  label: string
  /** The input kind. */
  type: DomainFieldType
  /** Whether the field must have a value before the operation goes ahead. */
  required: boolean
  /** Help text under the field; null for none. */
  description: string | null
  /** Hint in an empty input; null for none. */
  placeholder: string | null
  /** Regular expression the whole value must match; null for none. */
  pattern: string | null
  /** Fewest characters of a value; null for no lower bound. */
  minLength: number | null
  /** Most characters of a value; null for no upper bound. */
  maxLength: number | null
  /** The choices of a select field; empty for every other kind. */
  options: DomainFieldOption[]
}
