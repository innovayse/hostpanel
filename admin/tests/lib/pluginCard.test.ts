/**
 * Unit tests for the plugin card mappers: an installed plugin and a registry listing become the same card model.
 */
import { describe, expect, it } from 'vitest'
import { categoryLabel, fromInstalled, fromRegistry } from '@/lib/pluginCard'
import { PluginCardBadge, PluginCategory, PluginManifestRule, PluginPricing, PluginStatus } from '@/types/plugin'
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
  sdkVersion: '3.0.0',
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

  it('shows the installed version with Active, Staged and Update badges', () => {
    const card = fromRegistry(
      { ...listed, installed: true, installedVersion: '1.0.0', updateAvailable: true },
      '1.5.0',
    )

    expect(card.version).toBe('1.0.0')
    expect(card.badges).toEqual([PluginCardBadge.Active, PluginCardBadge.Staged, PluginCardBadge.Update])
    expect(card.updateVersion).toBe('2.0.0')
    expect(card.stagedVersion).toBe('1.5.0')
  })

  it('treats missing tags and downloads as none', () => {
    const card = fromRegistry({ ...listed, tags: null, downloads: undefined }, null)

    expect(card.tags).toEqual([])
    expect(card.downloads).toBeNull()
  })
})
