/**
 * Runtime configuration of the admin SPA.
 *
 * Filled per deployment by `public/config.js` (the prod image regenerates that file from the
 * container environment at start), so one built image runs on any domain and base path.
 */
export interface AppRuntimeConfig {
  /** Public base path the SPA is served under, e.g. `/` or `/admin`. */
  basePath: string
  /**
   * Public origin of the storefront, e.g. `https://host.example.com`, or `''` when unknown.
   * Image paths the storefront stores as root-relative (`/demo/<file>`) live there, not on the
   * admin's own origin.
   */
  clientBaseUrl: string
  /**
   * Public address of the documentation website, e.g. `https://innovayse.github.io/hostpanel/`.
   * Empty string means documentation links are hidden.
   */
  docsUrl: string
}
