/**
 * GET /api/portal/auth/sso/profile
 * Proxies to SSO GET /api/account/profile and returns user data.
 * Appends panel.local-specific menuItems so the widget can render
 * quick-links (Dashboard, Services, Domains, Invoices, Support) inside
 * the account popup under "Manage your Innovayse Account".
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const accessToken = getCookie(event, 'auth_token')
  if (!accessToken) throw createError({ statusCode: 401 })

  const profile = await $fetch<Record<string, unknown>>(`${config.ssoUrl}/api/account/profile`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  // Extract ssoSub from JWT for account-switching (widget needs it to call /api/portal/auth/sso/switch)
  let ssoSub = (profile.sub as string | undefined) || ''
  if (!ssoSub) {
    try {
      const payload = accessToken.split('.')[1] ?? ''
      const decoded = JSON.parse(Buffer.from(payload, 'base64url').toString('utf8'))
      ssoSub = (decoded.sub as string | undefined) || ''
    } catch { /* ignore */ }
  }

  return {
    ...profile,
    ssoSub,
    menuItems: [
      { label: 'Dashboard',   url: '/client/dashboard', icon: 'dashboard' },
      { label: 'My Services', url: '/client/services',  icon: 'services'  },
      { label: 'My Domains',  url: '/client/domains',   icon: 'domains'   },
      { label: 'Invoices',    url: '/client/invoices',  icon: 'invoices'  },
      { label: 'Support',     url: '/client/tickets',   icon: 'support'   },
    ],
  }
})
