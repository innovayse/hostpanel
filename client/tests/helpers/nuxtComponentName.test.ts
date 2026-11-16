/**
 * Tests for `tests/helpers/nuxtComponentName.ts`: the names must match the ones Nuxt auto-imports, or a mounted
 * component silently renders its children as unknown elements and the guard misses a stub of one of ours.
 *
 * @module tests/helpers/nuxtComponentName.test
 */
import { describe, expect, it } from 'vitest'
import { nuxtComponentName } from '~/tests/helpers/nuxtComponentName'

describe('nuxtComponentName', () => {
  it.each([
    [['client'], 'StatusBadge', 'ClientStatusBadge'],
    [['client', 'services'], 'ServiceCard', 'ClientServicesServiceCard'],
    [['client', 'services', 'setup'], 'SetupWizard', 'ClientServicesSetupWizard'],
    [['client', 'invoices'], 'PayPanel', 'ClientInvoicesPayPanel'],
    [['client', 'domains'], 'DomainSwitchRow', 'ClientDomainsDomainSwitchRow'],
    [['ui'], 'UiButton', 'UiButton'],
    [['layout'], 'Header', 'LayoutHeader'],
    [['sections'], 'SectionsFAQ', 'SectionsFAQ'],
    [[], 'Logo', 'Logo'],
  ])('names %j/%s as %s', (folders, file, expected) => {
    expect(nuxtComponentName(folders, file)).toBe(expected)
  })
})

