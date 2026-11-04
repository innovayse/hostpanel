/**
 * Fixtures for the integrations screens and the plugin manager: one payment integration, its
 * detail with a field list, and one installed plugin.
 */
import type { IntegrationDetailDto, IntegrationDto } from '../../src/types/integration'
import { IntegrationFieldType } from '../../src/types/integration'
import type { PluginListItemDto } from '../../src/types/plugin'
import { PluginStatus } from '../../src/types/plugin'

/** A disabled payment integration as the list endpoint returns it. */
export const stripe: IntegrationDto = {
  slug: 'stripe',
  name: 'Stripe',
  category: 'payments',
  isEnabled: false,
  isConfigured: false,
  isPlugin: false,
  pluginType: null,
  connection: null,
}

/** The same integration with the fields its configuration form renders. */
export const stripeDetail: IntegrationDetailDto = {
  slug: stripe.slug,
  name: stripe.name,
  isEnabled: stripe.isEnabled,
  config: {
    secretKey: { value: null, isSecret: true, hasValue: false },
    publishableKey: { value: '', isSecret: false, hasValue: false },
    webhookUrl: { value: '', isSecret: false, hasValue: false },
    timeoutSeconds: { value: '30', isSecret: false, hasValue: true },
  },
  fieldDefinitions: [
    { key: 'secretKey', label: 'Secret key', type: IntegrationFieldType.Password, required: true },
    { key: 'publishableKey', label: 'Publishable key', type: IntegrationFieldType.Text, required: false },
    { key: 'webhookUrl', label: 'Webhook URL', type: IntegrationFieldType.Url, required: false },
    { key: 'timeoutSeconds', label: 'Timeout (seconds)', type: IntegrationFieldType.Number, required: false, min: 1, max: 60 },
  ],
  canTestConnection: true,
  connection: null,
}

/** An installed, loaded provisioning plugin. */
export const cwpPlugin: PluginListItemDto = {
  id: 'innovayse-cwp',
  name: 'CWP provisioning',
  version: '1.0.0',
  author: 'Hostpanel',
  description: 'Provisions hosting accounts on CWP servers.',
  type: 'provisioning',
  category: 'Hosting / Provisioning',
  color: '#1a73e8',
  isLoaded: true,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}
