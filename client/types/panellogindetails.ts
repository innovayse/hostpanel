/**
 * How a person signs in by hand to the panel of one of their services, as the backend's `PanelLoginDetailsDto` sends it.
 * It never carries a password.
 *
 * @module types/panellogindetails
 */

/** The panel's own sign in page and the account's details, for a panel that cannot sign anybody in itself. */
export interface PanelLoginDetails {
  /** The user name the account signs in with. */
  username: string
  /** The primary domain of the account; empty when the panel reports none. */
  domain: string
  /** The address of the server the account lives on. */
  serverAddress: string
  /** The address of the panel's own sign in page, opened by the person in a new tab. */
  loginUrl: string
}
