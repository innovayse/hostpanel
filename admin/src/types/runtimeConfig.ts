/**
 * Runtime configuration of the admin SPA.
 *
 * Filled per deployment by `public/config.js` (the prod image regenerates that file from the
 * container environment at start), so one built image runs on any domain.
 */
export interface AppRuntimeConfig {
  /**
   * Public origin of the storefront, e.g. `https://host.example.com`, or `''` when unknown.
   * Image paths the storefront stores as root-relative (`/demo/<file>`) live there, not on the
   * admin's own origin.
   */
  clientBaseUrl: string
  /**
   * Public address of the documentation website, e.g. `https://hostpanel.innovayse.com/docs/`.
   * Empty string means documentation links are hidden.
   */
  docsUrl: string
}
