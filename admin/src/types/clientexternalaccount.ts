/**
 * The external account link of a client: the body `PUT /api/clients/{id}/external-account`
 * takes and the names of the fields the API can refuse.
 */

/** Wire names of the fields of the link, the keys the API's per-field messages come under. */
export enum ExternalAccountField {
  /** The name of the other system. */
  Provider = 'provider',
  /** The identifier the other system gives the client. */
  AccountId = 'accountId',
}

/** The account in another system a client is linked to, as the client detail carries it. */
export interface ClientExternalAccount {
  /** The name of the other system. */
  provider: string
  /** The identifier the other system gives the client. */
  id: string
}

/** Body of the set-or-clear call. Both members null clears the link; the API refuses one alone. */
export interface SetClientExternalAccountPayload {
  /** The name of the other system, or null (with a null account id) to clear the link. */
  provider: string | null
  /** The identifier the other system gives the client, or null (with a null provider) to clear the link. */
  accountId: string | null
}
