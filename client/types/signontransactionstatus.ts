/**
 * The outcome of looking up the transaction cookie of a returned `state`.
 *
 * @module types/signontransactionstatus
 */
export enum SignonTransactionStatus {
  /** A live, authentic transaction for this `state` was found. */
  Found = 'found',
  /** No cookie for this `state`: never started here, already used, or dropped by the browser. */
  Missing = 'missing',
  /** The cookie authenticated but is older than a sign-in attempt may last. */
  Expired = 'expired',
  /** The cookie did not authenticate, or authenticated but holds a different `state`: tampered or foreign. */
  Tampered = 'tampered'
}
