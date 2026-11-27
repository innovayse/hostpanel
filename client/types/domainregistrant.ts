/**
 * The registrant of a domain as the contact form loads it.
 *
 * @module types/domainregistrant
 */

import type { DomainContactFields } from '~/types/domaincontactfields'

/** The registrant contact the registrar holds and the extra values it holds beside it. */
export interface DomainRegistrant {
  /** The contact, as form fields. */
  contact: DomainContactFields
  /** The registry's extra values by field key, as last sent to the registrar; empty when it asked for none. */
  extendedAttributes: Record<string, string>
}
