/**
 * The kind of a sign-in attempt, sealed into its transaction cookie.
 *
 * @module types/signontransactionkind
 */
export enum SignonTransactionKind {
  /** A sign-in the visitor started and is completing on the provider's pages. */
  Interactive = 'interactive',
  /** A background `prompt=none` probe that asks the provider whether a session already exists. */
  Silent = 'silent'
}
