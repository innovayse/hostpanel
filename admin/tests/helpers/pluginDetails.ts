/**
 * Fixtures of the plugin details tests: one registry plugin with three versions (a newer one on
 * offer, the installed one, a yanked older one), built fresh per call so a test may change it.
 */
import { PluginCategory, PluginPricing, PluginVersionStatus } from '@/types/plugin'
import type { RegistryPluginDetailsDto, RegistryPluginVersionDto } from '@/types/plugin'

/**
 * Builds one version of the fixture plugin.
 *
 * @param overrides - Fields to change.
 * @returns A published, compatible version 1.0.0 with no notes and no permissions.
 */
export const makeVersion = (overrides: Partial<RegistryPluginVersionDto> = {}): RegistryPluginVersionDto => ({
  version: '1.0.0',
  publishedAt: '2026-01-15T10:00:00Z',
  status: PluginVersionStatus.Published,
  statusReason: null,
  replacementVersion: null,
  changelog: null,
  minHostVersion: null,
  maxHostVersion: null,
  compatible: true,
  permissions: [],
  screenshots: [],
  ...overrides,
})

/**
 * Builds the details of the fixture plugin: installed at 2.0.0, with 3.0.0 on offer.
 *
 * @param overrides - Fields to change.
 * @returns The details.
 */
export const makeDetails = (overrides: Partial<RegistryPluginDetailsDto> = {}): RegistryPluginDetailsDto => ({
  id: 'example-pay',
  name: 'Example Pay',
  author: 'Hostpanel',
  description: 'Card payments',
  iconUrl: null,
  category: PluginCategory.Payment,
  pricing: PluginPricing.Free,
  verified: true,
  latestVersion: '3.0.0',
  installed: true,
  installedVersion: '2.0.0',
  stagedVersion: null,
  updateAvailable: true,
  requiresLicence: false,
  hasLicenceKey: false,
  buyUrl: null,
  documentationUrl: 'https://docs.example.org/pay',
  supportUrl: 'https://support.example.org',
  repositoryUrl: null,
  licence: 'MIT',
  versions: [
    makeVersion({ version: '3.0.0', changelog: 'New checkout flow.\nFaster refunds.', permissions: ['payments.charge'] }),
    makeVersion({ version: '2.0.0' }),
    makeVersion({
      version: '1.0.0',
      status: PluginVersionStatus.Yanked,
      statusReason: 'Charges twice.',
      replacementVersion: '2.0.0',
    }),
  ],
  ...overrides,
})
