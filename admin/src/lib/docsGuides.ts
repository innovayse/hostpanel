import { AppRoute } from '@/lib/routes'

/** Guide path for the SSH key agent setup dialog (component-level, not tied to a route). */
export const SSH_KEY_AGENT_GUIDE = 'servers/ssh-key-agent'

/**
 * Maps each admin route to its documentation guide path (relative to the docs base URL).
 * Used by `useDocsGuide` to build the help link for a screen.
 *
 * Keep this map in one place: adding a guide for a new screen means one line here.
 */
export const DOCS_GUIDES: Partial<Record<string, string>> = {
  [AppRoute.Servers]: 'servers/servers',
  [AppRoute.IntegrationsPayments]: 'payments/gateways',
  [AppRoute.Migration]: 'migration/from-another-system',
  [AppRoute.SettingsGeneral]: 'staff/system-settings',
  [AppRoute.SettingsEmailTemplates]: 'staff/email-templates',
  [AppRoute.SettingsTldPricing]: 'staff/tld-pricing',
  [AppRoute.SettingsProducts]: 'staff/products',
  [AppRoute.SettingsProductBundles]: 'staff/products',
  [AppRoute.BillingInvoices]: 'staff/billing',
} as const
