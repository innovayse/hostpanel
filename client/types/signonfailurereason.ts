/**
 * The fixed reason code logged when a sign-in callback does not produce a session. Only these
 * values are logged: never the code, the state, a token or anything the provider said.
 *
 * @module types/signonfailurereason
 */
export enum SignonFailureReason {
  /** The attempt outlived its transaction cookie. */
  Expired = 'expired',
  /** No transaction cookie exists for the returned `state` (or no `state` was returned). */
  UnknownState = 'unknown_state',
  /** The transaction cookie failed authentication or holds a different `state`. */
  TamperedTransaction = 'tampered_transaction',
  /** The provider answered with an error instead of a code. */
  ProviderError = 'provider_error',
  /** The callback carried no code. */
  MissingCode = 'missing_code',
  /** The provider refused the code exchange, or could not be reached. */
  ExchangeFailed = 'exchange_failed',
  /** The callback matched no attempt, but the browser already holds a live session, so the visitor was passed on. */
  AlreadySignedIn = 'already_signed_in'
}
