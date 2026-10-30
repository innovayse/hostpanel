/**
 * Unit tests for the integration value sets: the strings the API stores and the category names it
 * groups integrations under.
 */
import { describe, expect, it } from 'vitest'
import { IntegrationCategoryName, IntegrationEncryption, IntegrationMode, IntegrationYesNo } from '@/types/integration'
import { INTEGRATION_CATEGORIES, INTEGRATION_META, PLUGIN_TYPE_LABELS } from '@/types/integrationmeta'
import { PluginType } from '@/types/plugin'

describe('integration value sets', () => {
  it('keep the strings the API stores', () => {
    expect(Object.values(IntegrationMode)).toEqual(['Live', 'Test', 'Sandbox'])
    expect(Object.values(IntegrationYesNo)).toEqual(['Yes', 'No'])
    expect(Object.values(IntegrationEncryption)).toEqual(['TLS', 'SSL', 'None'])
  })

  it('keep the category names the API groups integrations under', () => {
    expect(Object.values(IntegrationCategoryName)).toEqual([
      'Payment Gateways',
      'Domain Registrars',
      'Hosting / Provisioning',
      'Email / SMTP',
      'Fraud Protection',
    ])
  })

  it('keep the plugin type names the API sends', () => {
    expect(Object.values(PluginType)).toEqual(['Provisioning', 'Payment', 'Registrar', 'Identity', 'Fulfilment', 'Hosted'])
  })

  it('label every plugin type and list a page for each plugin-only category', () => {
    expect(Object.keys(PLUGIN_TYPE_LABELS)).toEqual(Object.values(PluginType))
    expect(PLUGIN_TYPE_LABELS[PluginType.Hosted]).toBe('Background worker')
    expect(INTEGRATION_CATEGORIES.map(category => category.key)).toEqual(
      ['payments', 'registrars', 'provisioning', 'email', 'fraud', 'fulfilment', 'identity', 'hosted'],
    )
    expect(INTEGRATION_CATEGORIES.at(-1)?.label).toBe('Background workers')
  })

  it('offer the payment modes through the enum', () => {
    const modeField = INTEGRATION_META.stripe.fields.find((field) => field.key === 'mode')

    expect(modeField?.options).toEqual([IntegrationMode.Live, IntegrationMode.Test])
  })
})
