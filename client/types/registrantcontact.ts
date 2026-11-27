/**
 * The WHOIS registrant contact the registrar currently holds for a domain, as
 * `GET /api/portal/client/domains/:id/contact` answers with it. Mirrors the backend
 * `DomainContactDto` one to one; the two optional lines arrive as null when the registrant has
 * none, unlike the form's {@link DomainContactFields}, which binds empty strings.
 */
export interface RegistrantContact {
  /** Registrant first name. */
  firstName: string
  /** Registrant last name. */
  lastName: string
  /** Organization name; null when the registrant has none. */
  organization: string | null
  /** Contact email address. */
  email: string
  /** Contact phone number. */
  phone: string
  /** Street address line 1. */
  address1: string
  /** Street address line 2; null when there is none. */
  address2: string | null
  /** City. */
  city: string
  /** State or province. */
  state: string
  /** Postal/zip code. */
  postalCode: string
  /** ISO 3166-1 alpha-2 country code. */
  country: string
  /** The registry's extra values of the domain by field key, as last sent to the registrar; empty when it asked for none. */
  extendedAttributes: Record<string, string>
}
