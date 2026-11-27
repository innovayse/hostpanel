/**
 * Unit tests for the installed plugin filter: the state a plugin is in, and the search, state and
 * category narrowing the Installed tab applies.
 */
import { describe, expect, it } from 'vitest'
import { filterInstalledPlugins, INSTALLED_PLUGIN_STATE_LOOK, InstalledPluginState, installedPluginState } from '@/lib/installedPlugins'
import { PluginManifestRule, PluginStatus } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { UiTone } from '@/utils/ui/tone'

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
  isEnabled: true,
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

  it('tells a loaded plugin whose enabled switch is off from an active one', () => {
    expect(installedPluginState(makePlugin({ isEnabled: false }))).toBe(InstalledPluginState.Disabled)
    expect(installedPluginState(makePlugin({ isEnabled: true }))).toBe(InstalledPluginState.Active)
    expect(installedPluginState(makePlugin({ isEnabled: false, isLoaded: false }))).toBe(InstalledPluginState.PendingRestart)
  })

  it('tells an installed plugin whose assembly failed to load from a refused one, whatever its listing status', () => {
    const failed = { isLoaded: false, refusedRule: PluginManifestRule.AssemblyLoadFailed, refusedDetail: 'Could not load file or assembly' }

    expect(installedPluginState(makePlugin(failed))).toBe(InstalledPluginState.LoadFailed)
    expect(installedPluginState(makePlugin({ ...failed, status: PluginStatus.Refused }))).toBe(InstalledPluginState.LoadFailed)
  })

  it('treats any other broken rule as a refusal, not a load failure', () => {
    const refused = { status: PluginStatus.Refused, isLoaded: false, refusedRule: PluginManifestRule.SdkNewerThanHost }

    expect(installedPluginState(makePlugin(refused))).toBe(InstalledPluginState.Refused)
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

describe('INSTALLED_PLUGIN_STATE_LOOK', () => {
  it('names and colours every state once, with a locale key of its own', () => {
    const looks = Object.values(InstalledPluginState).map(state => INSTALLED_PLUGIN_STATE_LOOK[state])

    expect(new Set(looks.map(look => look.labelKey)).size).toBe(looks.length)
    expect(INSTALLED_PLUGIN_STATE_LOOK[InstalledPluginState.Active].tone).toBe(UiTone.Success)
    expect(INSTALLED_PLUGIN_STATE_LOOK[InstalledPluginState.PendingRestart].tone).toBe(UiTone.Warning)
    expect(INSTALLED_PLUGIN_STATE_LOOK[InstalledPluginState.Refused].tone).toBe(UiTone.Danger)
  })
})
