/**
 * Fixtures for the plugin registry browser: one verified free plugin the registry lists and has
 * not been installed yet, the trust setting row, and the installed row the install leaves behind.
 */
import type { PluginListItemDto, PluginRegistryItemDto } from '../../src/types/plugin'
import { PluginCategory, PluginPricing, PluginRegistryTrust, PluginStatus } from '../../src/types/plugin'
import { PluginSettingKey } from '../../src/types/setting'
import type { Setting } from '../../src/types/setting'

/** A verified free plugin as the registry listing returns it, not installed on this server. */
export const registryGateway: PluginRegistryItemDto = {
  id: 'sample-gateway',
  name: 'Sample gateway',
  author: 'Example Publisher',
  description: 'Takes card payments through a sample processor.',
  iconUrl: null,
  category: PluginCategory.Payment,
  latestVersion: '1.2.0',
  sdkVersion: '3.1.0',
  verified: true,
  pricing: PluginPricing.Free,
  installed: false,
  installedVersion: null,
  updateAvailable: false,
}

/** The same listing once the install has put the plugin on this server. */
export const registryGatewayInstalled: PluginRegistryItemDto = {
  ...registryGateway,
  installed: true,
  installedVersion: registryGateway.latestVersion,
}

/** The installed plugin row the plugin list returns after the install (not loaded until a restart). */
export const installedGateway: PluginListItemDto = {
  id: registryGateway.id,
  name: registryGateway.name,
  version: registryGateway.latestVersion,
  author: registryGateway.author,
  description: registryGateway.description,
  type: 'payment',
  category: 'Payments',
  color: '#0f766e',
  isLoaded: false,
  iconUrl: null,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}

/** The registry trust setting row at its default. */
export const registryTrustRow: Setting = {
  id: 11,
  key: PluginSettingKey.RegistryTrust,
  value: PluginRegistryTrust.VerifiedOnly,
}
