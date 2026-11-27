/**
 * The kind of registrant a set of extra domain fields applies to, as the API names it in its query.
 *
 * @module types/domainfieldholder
 */
export enum DomainFieldHolder {
  /** Not said: only the fields declared for every registrant apply. */
  Any = 'any',
  /** A private person. */
  Individual = 'individual',
  /** A company or other organisation. */
  Organization = 'organization'
}
