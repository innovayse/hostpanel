/**
 * The deployment's authentication mode, and the sign-in destination that follows
 * from it.
 *
 * hostpanel ships two authentication paths and a deployment runs exactly one of
 * them:
 *
 * - `sso`   — Innovayse SSO is the identity provider. Signing in means starting
 *             the OIDC code flow at `/api/portal/auth/sso/authorize`, a Nitro
 *             route that mints PKCE state and redirects the browser on to
 *             `SSO_PUBLIC_URL/connect/authorize`. The local form is never used.
 * - `local` — the standalone open-source path: hostpanel authenticates users
 *             from its own table through `/client/login`. This is the mode the
 *             imported WHMCS client accounts sign in under, so the page stays.
 *
 * The mode arrives as `runtimeConfig.public.authMode` (env `AUTH_MODE`, passed
 * to the client container as `NUXT_PUBLIC_AUTH_MODE`). This composable exists so
 * that neither the literal `'sso'` nor the authorize path is written into a
 * component: `options-and-configuration.md` — "Nothing reads a settings key by
 * string at the point of use." Before it, two headers each carried their own
 * copy and a third silently hard-coded `/client/login`.
 *
 * @returns The active mode, an `isSso` flag, and the sign-in destination.
 */
export const useAuthMode = () => {
  const config = useRuntimeConfig()
  const localePath = useLocalePath()

  /**
   * Active authentication mode. Falls back to `sso`, which is what
   * `nuxt.config.ts` defaults to and what every Innovayse-operated tier runs;
   * a standalone install sets `AUTH_MODE=local`.
   */
  const mode = computed<string>(() => (config.public.authMode as string) || 'sso')

  /** True when the platform SSO is the identity provider for this deployment. */
  const isSso = computed<boolean>(() => mode.value === 'sso')

  /**
   * Where a signed-out visitor is sent to sign in.
   *
   * Under `sso` this is a server route, not a page — it must be reached with a
   * real browser navigation (`<a href>`), because Vue Router would try to match
   * it against the client bundle and find nothing. It is deliberately not the
   * SSO host itself: the authorize route is what generates the PKCE verifier and
   * CSRF state, and skipping it would produce an authorization request the
   * callback cannot complete.
   *
   * Under `local` it is the localised `/client/login` page.
   */
  const signInHref = computed<string>(() =>
    isSso.value ? '/api/portal/auth/sso/authorize' : localePath('/client/login'))

  return { mode, isSso, signInHref }
}
