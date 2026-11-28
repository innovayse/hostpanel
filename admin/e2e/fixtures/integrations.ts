/**
 * Fixtures for the integrations screens and the plugin manager: one payment integration, its
 * detail with a field list, and one installed plugin.
 */
import type { IntegrationDetailWireDto, IntegrationWireDto } from '../../src/types/integration'
import { IntegrationFieldType } from '../../src/types/integration'
import type { PluginListItemDto } from '../../src/types/plugin'
import { PluginStatus } from '../../src/types/plugin'

/** The name the bank transfer integration is listed under. */
export const bankTransferName = 'Bank Transfer'

/** A disabled payment integration as the list endpoint returns it. */
export const bankTransfer: IntegrationWireDto = {
  slug: 'bank-transfer',
  name: bankTransferName,
  category: 'payments',
  isEnabled: false,
  isConfigured: false,
  isPlugin: false,
  pluginType: null,
  iconUrl: null,
  connection: null,
  description: null,
}

/** The same integration with the fields its configuration form renders. */
export const bankTransferDetail: IntegrationDetailWireDto = {
  slug: bankTransfer.slug,
  name: bankTransfer.name,
  isEnabled: bankTransfer.isEnabled,
  config: {
    accountHolder: { value: '', isSecret: false, hasValue: false },
    iban: { value: '', isSecret: false, hasValue: false },
    webhookUrl: { value: '', isSecret: false, hasValue: false },
    timeoutSeconds: { value: '30', isSecret: false, hasValue: true },
  },
  fieldDefinitions: [
    { key: 'accountHolder', label: 'Account holder', type: IntegrationFieldType.Text, required: true },
    { key: 'iban', label: 'IBAN', type: IntegrationFieldType.Text, required: false },
    { key: 'webhookUrl', label: 'Webhook URL', type: IntegrationFieldType.Url, required: false },
    { key: 'timeoutSeconds', label: 'Timeout (seconds)', type: IntegrationFieldType.Number, required: false, min: 1, max: 60 },
  ],
  canTestConnection: true,
  connection: null,
}

/** An installed, loaded provisioning plugin. */
export const panelPlugin: PluginListItemDto = {
  id: 'example-panel',
  name: 'Panel provisioning',
  version: '1.0.0',
  author: 'Hostpanel',
  description: 'Provisions hosting accounts on panel servers.',
  type: 'provisioning',
  category: 'Hosting / Provisioning',
  color: '#1a73e8',
  isLoaded: true,
  isEnabled: true,
  iconUrl: null,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}
