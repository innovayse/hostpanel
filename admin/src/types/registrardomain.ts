/**
 * The domains a registrar account holds and the import of one of them into a client.
 */

/** One domain a registrar plugin reports for its account. */
export interface RegistrarDomain {
  /** The fully qualified domain name. */
  name: string
  /** ISO 8601 expiry the registrar reports; null when it does not say, and the domain then cannot be imported. */
  expiresAt: string | null
  /** Whether the panel already has a domain of that name, whichever client owns it. */
  inPanel: boolean
}

/** One page of a registrar's domain list. */
export interface RegistrarDomainPage {
  /** The domains of the page. */
  domains: RegistrarDomain[]
  /** The cursor of the next page, or null on the last one. */
  nextCursor: string | null
}

/** What an import sends: the client that will own the domain and the name to bring in. */
export interface ImportRegistrarDomainPayload {
  /** The client that will own the domain. */
  clientId: number
  /** The fully qualified domain name, as the registrar's list shows it. */
  domainName: string
}
