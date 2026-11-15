/**
 * The BFF paths of the client domain endpoints: the list, the transfer order and the actions on
 * the domain every panel test opens, so a test that answers them from a stub BFF route table names
 * an endpoint instead of spelling out the URL. The account reads a panel also makes are in
 * `accountPaths.ts`.
 *
 * @module tests/helpers/domainPanelPaths
 */

/** The domain endpoints that do not belong to a single domain. */
export const DomainEndpoint = {
  List: '/api/portal/client/domains',
  TransferOrder: '/api/portal/client/domains/transfer-order',
} as const

/** The id of the domain every panel test opens. */
export const DOMAIN_ID = '7'

/** The actions a panel performs on a domain, each the last segment of its path. */
export const DomainAction = {
  AutoRenew: 'autorenew',
  Lock: 'lock',
  IdProtect: 'idprotect',
  Nameservers: 'nameservers',
  Whois: 'whois',
  Contact: 'contact',
  Epp: 'epp',
  RenewOrder: 'renew-order',
} as const

/** One of the {@link DomainAction} values, or an empty string for the domain itself. */
type DomainActionPath = typeof DomainAction[keyof typeof DomainAction] | ''

/**
 * The path of an action on the domain every panel test opens.
 *
 * @param action - The action, or an empty string for the domain itself.
 * @returns The BFF path.
 */
export const domainPath = (action: DomainActionPath): string =>
  `${DomainEndpoint.List}/${DOMAIN_ID}${action === '' ? '' : `/${action}`}`
