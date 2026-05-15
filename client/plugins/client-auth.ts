/**
 * Client auth initialisation plugin — runs on CLIENT SIDE ONLY
 *
 * After Nuxt hydrates on the browser, if the `whmcs_authed` cookie is present
 * (meaning the user logged in previously), we fetch their profile once and
 * store it in `useState('whmcs_user')`.
 *
 * This ensures the client layout sidebar shows the user name/email immediately
 * on any client-side navigation without each page needing to fetch it separately.
 *
 * Note: `import.meta.client` guard makes this a no-op on the server,
 * preventing a redundant WHMCS API call during SSR.
 */
export default defineNuxtPlugin(async () => {
  if (!import.meta.client) return

  const { fetchUser, isLoggedIn } = useClientAuth()

  // Only fetch if a session cookie exists
  if (isLoggedIn.value) {
    await fetchUser()
  }
})
