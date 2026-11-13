/**
 * Unit tests for the installed plugin filter: the state a plugin is in, and the search, state and
 * category narrowing the Installed tab applies.
 */
import { describe, expect, it } from 'vitest'
import { filterInstalledPlugins, InstalledPluginState, installedPluginState } from '@/lib/installedPlugins'
import { PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'

/**
 * Builds an installed plugin row; the overrides replace the defaults.
 *
 * @param overrides - Fields to change.
 * @returns A loaded plugin.
 */
const makePlugin = (overrides: Partial<PluginListItemDto> = {}): PluginListItemDto => ({
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.0.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: '#123456',
  iconUrl: null,
  isLoaded: true,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
  ...overrides,
} as PluginListItemDto)

/** No criterion set. */
const ANY = { search: '', state: '', category: '' }

describe('installedPluginState', () => {
  it('tells a refused, a pending and an active plugin apart', () => {
    expect(installedPluginState(makePlugin({ status: PluginStatus.Refused, isLoaded: false }))).toBe(InstalledPluginState.Refused)
    expect(installedPluginState(makePlugin({ isLoaded: false }))).toBe(InstalledPluginState.PendingRestart)
    expect(installedPluginState(makePlugin())).toBe(InstalledPluginState.Active)
  })
})

describe('filterInstalledPlugins', () => {
  const pay = makePlugin()
  const mail = makePlugin({ id: 'example-mail', name: 'Mailer', description: 'Sends email', category: 'Email', isLoaded: false })
  const broken = makePlugin({ id: 'broken', name: 'Broken', description: 'Refused one', status: PluginStatus.Refused, isLoaded: false })
  const all = [pay, mail, broken]

  it('keeps every plugin when no criterion is set', () => {
    expect(filterInstalledPlugins(all, ANY)).toEqual(all)
  })

  it('matches the search text against name, id and description, ignoring case', () => {
    expect(filterInstalledPlugins(all, { ...ANY, search: 'MAILER' })).toEqual([mail])
    expect(filterInstalledPlugins(all, { ...ANY, search: 'example-' })).toEqual([pay, mail])
    expect(filterInstalledPlugins(all, { ...ANY, search: 'card' })).toEqual([pay])
  })

  it('narrows by state and by category, and combines the criteria', () => {
    expect(filterInstalledPlugins(all, { ...ANY, state: InstalledPluginState.Refused })).toEqual([broken])
    expect(filterInstalledPlugins(all, { ...ANY, category: 'Email' })).toEqual([mail])
    expect(filterInstalledPlugins(all, { search: 'example', state: InstalledPluginState.PendingRestart, category: 'Email' })).toEqual([mail])
    expect(filterInstalledPlugins(all, { search: 'nothing', state: '', category: '' })).toEqual([])
  })
})
