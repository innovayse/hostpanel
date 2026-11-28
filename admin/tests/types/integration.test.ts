/**
 * Unit tests for the integration value sets: the strings the API stores and the category names it
 * groups integrations under.
 */
import { describe, expect, it } from 'vitest'
import { IntegrationCategoryName, IntegrationEncryption, IntegrationMode, IntegrationYesNo } from '@/types/integration'
import { INTEGRATION_CATEGORIES, INTEGRATION_CATEGORY_BY_KEY, integrationMetaOf } from '@/types/integrationmeta'
import { translate } from '@/lib/translate'
import { PluginCategory, PluginType } from '@/types/plugin'

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

  it('list the categories in menu order, each with a title, a short menu caption and a description', () => {
    expect(INTEGRATION_CATEGORIES.map(category => category.key)).toEqual(
      ['payments', 'provisioning', 'registrars', 'email', 'fulfilment', 'fraud', 'identity', 'hosted'],
    )
    expect(INTEGRATION_CATEGORIES.map(category => translate(category.menuLabelKey))).toEqual(
      ['Payments', 'Hosting panels', 'Registrars', 'Email', 'Fulfilment', 'Fraud', 'Identity', 'Background jobs'],
    )
    expect(translate(INTEGRATION_CATEGORY_BY_KEY.hosted.labelKey)).toBe('Background jobs')
    expect(translate(INTEGRATION_CATEGORY_BY_KEY.identity.descriptionKey)).toContain('Identity plugins')
  })

  it('point a category at the registry category that lists its plugins, when there is one', () => {
    expect(INTEGRATION_CATEGORY_BY_KEY.payments.registryCategory).toBe(PluginCategory.Payment)
    expect(INTEGRATION_CATEGORY_BY_KEY.provisioning.registryCategory).toBe(PluginCategory.Provisioning)
    expect(INTEGRATION_CATEGORY_BY_KEY.registrars.registryCategory).toBe(PluginCategory.Registrar)
    expect(INTEGRATION_CATEGORY_BY_KEY.identity.registryCategory).toBeUndefined()
  })

  it('offer the encryption modes through the enum', () => {
    const encryptionField = integrationMetaOf('smtp')?.fields.find((field) => field.key === 'encryption')

    expect(encryptionField?.options?.map(option => option.value)).toEqual([IntegrationEncryption.None])
  })
})
