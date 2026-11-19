/**
 * The routed screens that have no skeleton schema of their own, each with the reason.
 *
 * A screen has a schema when its shape is known before its code has arrived. These do not: their content is decided
 * by data or by someone else, so while their chunk downloads the shell draws the generic `ViewLoading` stand-in for
 * them. The list is checked against the router (`tests/lint/skeletonRoutes.test.ts`): a routed screen is either in
 * the registry or named here, and an entry whose screen has a schema, or is no longer routed, fails the check.
 */

/** One routed screen that is exempt from having a skeleton schema. */
export interface SkeletonException {
  /** The screen's file under `src/`, such as `views/LoginView.vue`. */
  file: string
  /** Why the screen's shape is not known while it loads. */
  reason: string
}

/** The exempt screens. */
export const SKELETON_EXCEPTIONS: readonly SkeletonException[] = [
  { file: 'views/LoginView.vue', reason: 'the sign-in form depends on the deployment (local accounts or single sign-on) which is read first' },
  { file: 'views/SetupView.vue', reason: 'the first-run setup form depends on the account mode the server reports once loaded' },
  { file: 'views/VerifyEmailView.vue', reason: 'a one-line outcome of the token in the address, nothing to lay out beforehand' },
  { file: 'views/ForbiddenView.vue', reason: 'a short refusal notice drawn outside the shell, with nothing to wait for' },
  { file: 'views/NotFoundView.vue', reason: 'a short notice for an address that matches no screen, with nothing to wait for' },
  { file: 'views/DisputesView.vue', reason: 'a short "coming soon" notice with nothing to wait for' },
  { file: 'views/ManageAffiliatesView.vue', reason: 'a short "coming soon" notice with nothing to wait for' },
  { file: 'views/ServiceAddonsView.vue', reason: 'a short "coming soon" notice with nothing to wait for' },
  { file: 'views/IntegrationsView.vue', reason: 'the cards are the integrations of the category in the address, which only the server knows (a category may hold none and offer the plugin catalogue instead)' },
  { file: 'views/PluginsView.vue', reason: 'the tabs offered depend on whether the plugin registry is switched on, which the server reports once loaded' },
  { file: 'views/NotificationsView.vue', reason: 'the feed is whatever events the server holds: a list, an empty message or a refusal, none of which is known beforehand' },
  { file: 'views/PluginPageView.vue', reason: 'the blocks of a plugin page are defined by the plugin\'s own view document, which has not arrived yet' },
]
