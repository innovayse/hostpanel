/**
 * Unit tests for the plugin card mappers: an installed plugin and a registry listing become the same card model.
 */
import { describe, expect, it } from 'vitest'
import { INSTALLED_PLUGIN_STATE_LOOK, InstalledPluginState } from '@/lib/installedPlugins'
import { categoryLabel, fromInstalled, fromRegistry, stateLookOfBadge } from '@/lib/pluginCard'
import { PluginCardBadge, PluginCategory, PluginChangeResult, PluginManifestRule, PluginPricing, PluginStatus } from '@/types/plugin'
import type { PluginListItemDto, PluginRegistryItemDto } from '@/types/plugin'

/** An installed plugin that is running. */
const installed: PluginListItemDto = {
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.2.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#123456',
  iconUrl: '/icon.png',
  isLoaded: true,
  isBuiltIn: false,
  isEnabled: true,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
}

/** A registry listing that is not installed. */
const listed: PluginRegistryItemDto = {
  id: 'example-pay',
  name: 'Example Pay',
  author: 'Hostpanel',
  description: 'Card payments',
  iconUrl: null,
  category: PluginCategory.Payment,
  latestVersion: '2.0.0',
  sdkPackageVersion: '3.0.0',
  verified: true,
  pricing: PluginPricing.Paid,
  installed: false,
  installedVersion: null,
  updateAvailable: false,
  tags: ['cards'],
  downloads: 50,
}

describe('categoryLabel', () => {
  it('translates a registry category and leaves a manifest label as written', () => {
    expect(categoryLabel(PluginCategory.Provisioning)).toBe('Provisioning')
    expect(categoryLabel('Hosting / Provisioning')).toBe('Hosting / Provisioning')
  })

  it('is what both mappers show, so an installed and a registry card read the same', () => {
    expect(fromInstalled({ ...installed, category: PluginCategory.Payment }, null).category)
      .toBe(fromRegistry(listed, null).category)
  })
})

describe('fromInstalled', () => {
  it('adds a ChangeFailed badge and the reason and restored version when the last change failed', () => {
    const card = fromInstalled({ ...installed, lastChange: { pluginId: 'example-pay', fromVersion: '1.0.0', toVersion: '2.0.0', result: PluginChangeResult.Failed, error: 'Missing dependency.', restoredVersion: '1.0.0', at: '2026-11-24T10:00:00Z' } }, null)

    expect(card.badges).toEqual([PluginCardBadge.Active, PluginCardBadge.ChangeFailed])
    expect(card.failedChange).toEqual({ error: 'Missing dependency.', restoredVersion: '1.0.0' })
  })

  it('shows no failure for an applied change', () => {
    const card = fromInstalled({ ...installed, lastChange: { pluginId: 'example-pay', fromVersion: '1.0.0', toVersion: '2.0.0', result: PluginChangeResult.Applied, error: null, restoredVersion: null, at: '2026-11-24T10:00:00Z' } }, null)

    expect(card.badges).toEqual([PluginCardBadge.Active])
    expect(card.failedChange).toBeNull()
  })

  it('does not call a loaded plugin whose enabled switch is off Active', () => {
    expect(fromInstalled({ ...installed, isEnabled: false }, null).badges).toEqual([PluginCardBadge.Disabled])
  })

  it('maps a running plugin to an Active card', () => {
    const card = fromInstalled(installed, null)

    expect(card).toMatchObject({
      id: 'example-pay',
      publisher: 'Hostpanel',
      category: 'Payments',
      color: '#123456',
      iconUrl: '/icon.png',
      version: '1.2.0',
      badges: [PluginCardBadge.Active],
      refusal: null,
      failedChange: null,
    })
  })

  it('adds Staged, Update and BuiltIn badges in order', () => {
    const card = fromInstalled({ ...installed, isLoaded: false, isBuiltIn: true, stagedVersion: '1.3.0' }, '2.0.0')

    expect(card.badges).toEqual([PluginCardBadge.PendingRestart, PluginCardBadge.Staged, PluginCardBadge.Update, PluginCardBadge.BuiltIn])
    expect(card.stagedVersion).toBe('1.3.0')
    expect(card.updateVersion).toBe('2.0.0')
  })

  it('gives a refused plugin only the Refused badge and its broken rule', () => {
    const card = fromInstalled(
      { ...installed, status: PluginStatus.Refused, refusedRule: PluginManifestRule.RouteDuplicate, refusedDetail: 'GET /a' },
      '2.0.0',
    )

    expect(card.badges).toEqual([PluginCardBadge.Refused])
    expect(card.updateVersion).toBeNull()
    expect(card.refusal).toEqual({ rule: PluginManifestRule.RouteDuplicate, detail: 'GET /a' })
  })

  it('gives a plugin whose assembly failed to load only the LoadFailed badge and the load error as the rule detail', () => {
    const card = fromInstalled(
      { ...installed, isLoaded: false, refusedRule: PluginManifestRule.AssemblyLoadFailed, refusedDetail: 'Could not load file or assembly' },
      '2.0.0',
    )

    expect(card.badges).toEqual([PluginCardBadge.LoadFailed])
    expect(card.updateVersion).toBeNull()
    expect(card.refusal).toEqual({ rule: PluginManifestRule.AssemblyLoadFailed, detail: 'Could not load file or assembly' })
  })
})

describe('fromRegistry', () => {
  it('maps a listing with translated category and price and no badge', () => {
    const card = fromRegistry(listed, null)

    expect(card).toMatchObject({
      verified: true,
      category: 'Payment',
      pricingLabel: 'Paid',
      tags: ['cards'],
      downloads: 50,
      version: '2.0.0',
      badges: [],
      color: null,
    })
  })

  it('shows the installed version with the installed state, Staged and Update badges', () => {
    const card = fromRegistry(
      { ...listed, installed: true, installedVersion: '1.0.0', updateAvailable: true },
      { ...installed, stagedVersion: '1.5.0' },
    )

    expect(card.version).toBe('1.0.0')
    expect(card.badges).toEqual([PluginCardBadge.Active, PluginCardBadge.Staged, PluginCardBadge.Update])
    expect(card.updateVersion).toBe('2.0.0')
    expect(card.stagedVersion).toBe('1.5.0')
  })

  it('wears the same state badge as the installed card of the same plugin', () => {
    const item = { ...listed, installed: true, installedVersion: '1.2.0' }
    const rows = [
      installed,
      { ...installed, isEnabled: false },
      { ...installed, isLoaded: false },
      { ...installed, isLoaded: false, refusedRule: PluginManifestRule.AssemblyLoadFailed, refusedDetail: 'Could not load' },
    ]

    for (const row of rows) {
      expect(fromRegistry(item, row).badges[0]).toBe(fromInstalled(row, null).badges[0])
    }
  })

  it('treats missing tags and downloads as none', () => {
    const card = fromRegistry({ ...listed, tags: null, downloads: undefined }, null)

    expect(card.tags).toEqual([])
    expect(card.downloads).toBeNull()
  })
})

describe('stateLookOfBadge', () => {
  it('gives a state badge the look of its state', () => {
    expect(stateLookOfBadge(PluginCardBadge.Active)).toBe(INSTALLED_PLUGIN_STATE_LOOK[InstalledPluginState.Active])
    expect(stateLookOfBadge(PluginCardBadge.LoadFailed)).toBe(INSTALLED_PLUGIN_STATE_LOOK[InstalledPluginState.LoadFailed])
  })

  it('gives no look to a badge that is not a state', () => {
    expect(stateLookOfBadge(PluginCardBadge.Update)).toBeNull()
    expect(stateLookOfBadge(PluginCardBadge.Staged)).toBeNull()
    expect(stateLookOfBadge(PluginCardBadge.ChangeFailed)).toBeNull()
    expect(stateLookOfBadge(PluginCardBadge.BuiltIn)).toBeNull()
  })
})
