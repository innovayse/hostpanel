/**
 * Machine-readable error codes that the portal branches on.
 *
 * Kept apart from `lib/errorHandler.ts` because the Nitro BFF also needs one of them
 * (`accept-invite.post.ts` writes it) and server code must not import the client-side reader,
 * which reaches for the Nuxt app instance to translate its one fallback sentence.
 *
 * @module constants/portalErrorCode
 */

/**
 * Codes the API (or the BFF) sends, that something branches on, and the codes the BFF's own refusals carry in `data.code`.
 *
 * SCREAMING_SNAKE, matching the backend. These strings cross the wire and are part of the
 * contract: renaming one here without renaming it at its source silently turns the branch off,
 * with no error and no failing type-check. A code the API sends that merely needs displaying needs no
 * entry, because displaying it means rendering `detail` and nothing else; a code the BFF writes itself
 * has one entry, here, so that no route keeps a private copy of it.
 */
export const PortalErrorCode = {
  /**
   * The signed-in identity has no row in the backend's `clients` table.
   *
   * Not a failure. Staff identities, the platform superadmin above all, authenticate fine and
   * were simply never onboarded as customers, so every "my" endpoint answers 404 with this
   * code. `stores/client.ts` turns it into a state flag so the portal renders an explanation and
   * a way out instead of a red alert; the explanation itself is the sentence the API sent.
   */
  ClientProfileNotFound: 'CLIENT_PROFILE_NOT_FOUND',

  /**
   * An invitation was opened by someone who is not signed in.
   *
   * Not a failure either, and not an expired session: the invitation mail lands in a mail client,
   * so arriving with no credential is the normal first visit. **This one code is written by the
   * BFF, not by the C# API** (`server/api/portal/auth/accept-invite.post.ts` refuses before any
   * request to the backend is possible), so the backend has no resource entry for it and its
   * sentence stays in `client.acceptInvite.signInRequired`. That is the second client-authored
   * sentence the localization rule allows: a guard before any request is possible.
   */
  InviteSignInRequired: 'INVITE_SIGN_IN_REQUIRED',

  /**
   * The service a client asked for is not one of theirs, or does not exist.
   *
   * Written by the BFF (`server/api/portal/client/services/[id]/index.get.ts`, which picks the one
   * service out of the client's list) with no sentence; the service page branches on it to say
   * "not found" instead of showing a failed read with a retry.
   */
  ServiceNotFound: 'SERVICE_NOT_FOUND',

  /**
   * The plugin page an address names is not one the reader may open: an unknown, disabled or unconfigured plugin, an
   * undeclared page and a permission not held all answer this one code, so the page says "not found" with the API's
   * sentence rather than offering a retry that cannot help.
   */
  PluginPageNotFound: 'PLUGIN_PAGE_NOT_FOUND',

  /** The panel's own server refused a malformed address or id before asking the API (`requireRouteParam`). */
  BadRouteParam: 'BAD_ROUTE_PARAM',

  /**
   * A storefront file route was asked for a method it does not answer (`defineReadOnlyEventHandler` answers 405 with an
   * `Allow` header for anything but `GET` and `HEAD`). Written by the panel's own server, with no sentence.
   */
  MethodNotAllowed: 'METHOD_NOT_ALLOWED',

  /** The widget relay route was asked for a file name it does not serve (`server/routes/widget/[file].ts`). */
  WidgetFileNotFound: 'WIDGET_FILE_NOT_FOUND',
} as const

/** Any of the codes in {@link PortalErrorCode}. */
export type PortalErrorCodeValue = typeof PortalErrorCode[keyof typeof PortalErrorCode]
