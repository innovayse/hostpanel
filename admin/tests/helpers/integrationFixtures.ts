/**
 * Fixtures of the integrations tests: a summary and a detail as the admin API returns them.
 */
import type { IntegrationDetailDto, IntegrationDto } from '@/types/integration'
import { IntegrationFieldType } from '@/types/integration'

/**
 * Builds an integration summary.
 *
 * @param overrides - Fields to change on the default summary.
 * @returns The summary.
 */
export const integrationDto = (overrides: Partial<IntegrationDto> = {}): IntegrationDto => ({
  slug: 'stripe',
  name: 'Stripe',
  category: 'payments',
  isEnabled: true,
  isConfigured: true,
  isPlugin: false,
  pluginType: null,
  ...overrides,
})

/**
 * Builds an integration detail with one stored secret, one select, one textarea and one toggle
 * field. The secret carries no value, as the API sends it.
 *
 * @param overrides - Fields to change on the default detail.
 * @returns The detail.
 */
export const integrationDetail = (overrides: Partial<IntegrationDetailDto> = {}): IntegrationDetailDto => ({
  slug: 'stripe',
  name: 'Stripe',
  isEnabled: true,
  config: {
    secretKey: { value: null, isSecret: true, hasValue: true },
    mode: { value: 'Test', isSecret: false, hasValue: true },
    note: { value: 'hello', isSecret: false, hasValue: true },
    live: { value: 'false', isSecret: false, hasValue: true },
  },
  fieldDefinitions: [
    { key: 'secretKey', label: 'Secret key', type: IntegrationFieldType.Password, required: true },
    { key: 'mode', label: 'Mode', type: IntegrationFieldType.Select, required: false, options: ['Live', 'Test'] },
    { key: 'note', label: 'Note', type: IntegrationFieldType.Textarea, required: false },
    { key: 'live', label: 'Live payments', type: IntegrationFieldType.Toggle, required: false },
  ],
  ...overrides,
})
