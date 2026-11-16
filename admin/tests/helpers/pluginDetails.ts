/**
 * Fixtures of the plugin details tests: the installed row of one plugin and its registry plugin with three versions (a newer one on
 * offer, the installed one, a yanked older one), built fresh per call so a test may change it.
 */
import { PluginCategory, PluginPricing, PluginStatus, PluginVersionStatus } from '@/types/plugin'
import type { PluginListItemDto, RegistryPluginDetailsDto, RegistryPluginVersionDto } from '@/types/plugin'

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

/**
 * Builds the installed row of the fixture plugin as this server lists it: loaded, not built in, version 2.0.0.
 *
 * @param overrides - Fields to change.
 * @returns The list item.
 */
export const makeInstalled = (overrides: Partial<PluginListItemDto> = {}): PluginListItemDto => ({
  id: 'example-pay',
  name: 'Example Pay',
  version: '2.0.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#1a73e8',
  iconUrl: null,
  isLoaded: true,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
  changelog: null,
  ...overrides,
})
