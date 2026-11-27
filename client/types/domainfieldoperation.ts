/**
 * The domain operations a registrar plugin can ask extra fields for, as the API names them in its query.
 *
 * @module types/domainfieldoperation
 */
export enum DomainFieldOperation {
  /** Registering a new domain. */
  Register = 'register',
  /** Transferring a domain in. */
  Transfer = 'transfer',
  /** Changing the registrant contact of a domain the registrar holds. */
  ContactChange = 'contactChange'
}
