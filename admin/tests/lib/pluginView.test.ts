/**
 * Unit tests for the plugin view helpers: route methods and filled paths, page values, fields and
 * conditions, the link rule, chat events, value formats and tones, the Add-ons menu and its icons.
 */
import { describe, expect, it } from 'vitest'
import {
  DEFAULT_PLUGIN_ICON,
  fillPluginPath,
  formatPluginValue,
  groupPluginBlocks,
  parsePluginChatResult,
  parsePluginChatStep,
  pluginAlertTone,
  pluginButtonVariant,
  pluginConditionHolds,
  pluginIconPath,
  pluginLinkParams,
  pluginLinkTarget,
  pluginMenuLinks,
  pluginPageValues,
  pluginRouteMethod,
  pluginRouteRequest,
  pluginUiTone,
  readPluginField,
} from '@/lib/pluginView'
import { HttpMethod } from '@/types/http'
import { PluginBlockGroupKind, PluginBlockKind, PluginBlockTone, PluginChatStepState, PluginValueFormat } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

describe('routes and paths', () => {
  it('reads the method of a reference in any case and refuses an unknown one', () => {
    expect(pluginRouteMethod({ method: 'Post', path: '/x' })).toBe(HttpMethod.Post)
    expect(pluginRouteMethod({ method: 'options', path: '/x' })).toBeUndefined()
    expect(pluginRouteMethod(undefined)).toBeUndefined()
  })

  it('fills and encodes parameters, and refuses what would leave the plugin prefix', () => {
    expect(fillPluginPath('/sites/{siteId}/chat', { siteId: 'a b' })).toBe('sites/a%20b/chat')
    expect(fillPluginPath('/', {})).toBe('')
    expect(fillPluginPath('/sites/{siteId}', {})).toBeNull()
    expect(fillPluginPath('/sites/{siteId}', { siteId: '..' })).toBeNull()
    expect(fillPluginPath('/../admin', {})).toBeNull()
    expect(fillPluginPath('/a%2e', {})).toBeNull()
    expect(fillPluginPath('/sites/{toString}', {})).toBeNull()
  })

  it('turns a reference into a request with its query', () => {
    expect(pluginRouteRequest({ method: 'get', path: '/sites' }, {}, { page: '2' })).toEqual({ method: HttpMethod.Get, url: 'sites?page=2' })
    expect(pluginRouteRequest({ method: 'trace', path: '/sites' }, {})).toBeNull()
  })

  it('reads page values from the address by the template', () => {
    expect(pluginPageValues('/sites/{siteId}', ['sites', '7'])).toEqual({ siteId: '7' })
    expect(pluginPageValues('/sites/{siteId}', ['sites'])).toEqual({})
  })
})

describe('fields and conditions', () => {
  it('reads a dotted field, own members only', () => {
    expect(readPluginField({ a: { b: 2 } }, 'a.b')).toBe(2)
    expect(readPluginField({ a: 1 }, undefined)).toEqual({ a: 1 })
    expect(readPluginField({}, 'constructor')).toBeUndefined()
  })

  it('holds a condition only on a strict match of a present field', () => {
    const route = { method: 'get', path: '/s' }
    expect(pluginConditionHolds({ route, field: 'n', equals: 1 }, { n: 1 })).toBe(true)
    expect(pluginConditionHolds({ route, field: 'n', equals: '1' }, { n: 1 })).toBe(false)
    expect(pluginConditionHolds({ route, field: 'n', equals: [2, 3] }, { n: 3 })).toBe(true)
    expect(pluginConditionHolds({ route, field: 'n', equals: null }, {})).toBe(false)
  })

  it('reads link parameters from a row and fails on a missing one', () => {
    expect(pluginLinkParams({ page: 'p', params: { siteId: 'id' } }, { id: 4 })).toEqual({ siteId: '4' })
    expect(pluginLinkParams({ page: 'p', params: { siteId: 'id' } }, {})).toBeNull()
  })
})

describe('pluginLinkTarget', () => {
  it('admits a host-relative path and an https address on an allowed origin', () => {
    expect(pluginLinkTarget('/clients/4', [])).toEqual({ href: '/clients/4', external: false })
    expect(pluginLinkTarget('https://a.example.com/x', ['https://*.example.com'])).toEqual({ href: 'https://a.example.com/x', external: true })
    expect(pluginLinkTarget('https://example.com/x', ['https://example.com'])).toEqual({ href: 'https://example.com/x', external: true })
  })

  it('refuses every other address', () => {
    for (const href of ['//evil.test', '/api/admin', '/x/../API/y', '/%61pi/x', 'http://example.com', 'https://user@example.com', 'javascript:alert(1)', 'https://example.com.evil.test', '/a\\b', 42]) {
      expect(pluginLinkTarget(href, ['https://example.com']), String(href)).toBeNull()
    }
    expect(pluginLinkTarget('https://example.com/', ['https://*.example.com'])).toBeNull()
  })
})

describe('chat events', () => {
  it('reads a step and refuses a stopped or malformed one', () => {
    expect(parsePluginChatStep({ id: 'a', label: 'Reading', state: 'running' })).toEqual({ key: 'a', label: 'Reading', state: PluginChatStepState.Running })
    expect(parsePluginChatStep({ label: 'Reading', state: 'done' })?.key).toBe('Reading')
    expect(parsePluginChatStep({ label: 'Reading', state: 'stopped' })).toBeNull()
    expect(parsePluginChatStep({ label: '', state: 'done' })).toBeNull()
  })

  it('reads a result card with its actions under the link and route rules', () => {
    const card = parsePluginChatResult({
      tone: 'success',
      title: 'Published',
      details: ['a', 3, ''],
      actions: [
        { label: 'Open', href: '/clients' },
        { label: 'Evil', href: 'https://evil.test' },
        { label: 'Undo', route: { method: 'post', path: '/sites/{siteId}/undo' }, body: { version: 2 }, confirm: 'Sure?', until: { route: { method: 'get', path: '/state' }, field: 'live', equalsAny: [1] } },
        { label: 'Read', route: { method: 'get', path: '/state' } },
        { label: 'Again', resend: true },
        { label: 'Hidden', href: '/x', visibleWhen: { route: { method: 'post', path: '/s' }, field: 'a', equalsAny: [1] } },
      ],
    }, { siteId: '7' }, [], ['state'])

    expect(card?.tone).toBe(PluginBlockTone.Success)
    expect(card?.details).toEqual(['a'])
    expect(card?.actions.map((action) => action.label)).toEqual(['Open', 'Undo', 'Again'])
    expect(card?.actions[1]?.block).toEqual({
      kind: PluginBlockKind.Action,
      label: 'Undo',
      route: { method: 'post', path: '/sites/{siteId}/undo' },
      body: { version: 2 },
      confirm: 'Sure?',
      refresh: ['state'],
      followUp: { refresh: ['state'], until: { route: { method: 'get', path: '/state' }, field: 'live', equals: [1] } },
    })
  })

  it('refuses a card with neither title nor text, and reads an unknown tone as information', () => {
    expect(parsePluginChatResult({ actions: [] }, {}, [])).toBeNull()
    expect(parsePluginChatResult({ text: 'x', tone: 'loud' }, {}, [])?.tone).toBe(PluginBlockTone.Info)
  })
})

describe('formats and tones', () => {
  it('formats a value as text', () => {
    expect(formatPluginValue(null, undefined, 'en')).toBe('—')
    expect(formatPluginValue({ amount: 3, currency: 'EUR' }, PluginValueFormat.Money, 'en')).toBe('€3.00')
    expect(formatPluginValue({ a: 1 }, undefined, 'en')).toBe('{"a":1}')
    expect(formatPluginValue(5, PluginValueFormat.Text, 'en')).toBe('5')
  })

  it('maps a plugin tone to the kit', () => {
    expect(pluginUiTone(PluginBlockTone.Danger)).toBe(UiTone.Danger)
    expect(pluginUiTone(undefined)).toBe(UiTone.Neutral)
    expect(pluginAlertTone(PluginBlockTone.Neutral)).toBe(UiTone.Info)
    expect(pluginAlertTone(PluginBlockTone.Warning)).toBe(UiTone.Warning)
    expect(pluginButtonVariant(PluginBlockTone.Primary)).toBe(UiButtonVariant.Primary)
    expect(pluginButtonVariant(PluginBlockTone.Danger)).toBe(UiButtonVariant.Danger)
    expect(pluginButtonVariant(undefined)).toBe(UiButtonVariant.Secondary)
  })
})

describe('the Add-ons menu', () => {
  it('orders the entries and leaves out a page that takes parameters or is missing', () => {
    const links = pluginMenuLinks([
      { pluginId: 'sites', id: 'b', label: 'Second', page: 'home', icon: 'chart', order: 2 },
      { pluginId: 'sites', id: 'a', label: 'First', page: 'usage', icon: null, order: 1 },
      { pluginId: 'sites', id: 'c', label: 'Param', page: 'site', icon: null, order: 0 },
      { pluginId: 'sites', id: 'd', label: 'Gone', page: 'none', icon: null, order: 0 },
    ], [
      { pluginId: 'sites', id: 'home', path: '/', title: 'Home' },
      { pluginId: 'sites', id: 'usage', path: '/usage', title: 'Usage' },
      { pluginId: 'sites', id: 'site', path: '/sites/{siteId}', title: 'Site' },
    ])

    expect(links).toEqual([
      { key: 'sites:a', label: 'First', icon: null, to: '/apps/sites/usage' },
      { key: 'sites:b', label: 'Second', icon: 'chart', to: '/apps/sites' },
    ])
  })

  it('maps an icon name to a built-in icon and anything else to the default', () => {
    expect(pluginIconPath('chart')).not.toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIconPath('https://evil.test/x.svg')).toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIconPath('__proto__')).toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIconPath(null)).toBe(DEFAULT_PLUGIN_ICON)
  })
})

describe('groupPluginBlocks', () => {
  it('groups consecutive stats and consecutive links and leaves every other block alone', () => {
    const groups = groupPluginBlocks([
      { kind: PluginBlockKind.Stat, label: 'A' },
      { kind: PluginBlockKind.Stat, label: 'B' },
      { kind: PluginBlockKind.Text, text: 't' },
      { kind: PluginBlockKind.Link, label: 'L1' },
      { kind: PluginBlockKind.Link, label: 'L2' },
      { kind: PluginBlockKind.Stat, label: 'C' },
    ])

    expect(groups.map((group) => [group.kind, group.blocks.length])).toEqual([
      [PluginBlockGroupKind.Stats, 2],
      [PluginBlockGroupKind.Single, 1],
      [PluginBlockGroupKind.Links, 2],
      [PluginBlockGroupKind.Stats, 1],
    ])
  })

  it('never groups two blocks of a kind that stands alone', () => {
    const groups = groupPluginBlocks([{ kind: PluginBlockKind.Text, text: 'a' }, { kind: PluginBlockKind.Text, text: 'b' }])

    expect(groups.map((group) => group.kind)).toEqual([PluginBlockGroupKind.Single, PluginBlockGroupKind.Single])
  })

  it('keys each block by its id or kind and its place, under the scope, so a repeated id stays unique', () => {
    const groups = groupPluginBlocks([
      { kind: PluginBlockKind.Stat, id: 'a', label: 'A' },
      { kind: PluginBlockKind.Stat, id: 'a', label: 'B' },
    ], 'tab:')

    expect(groups[0]?.key).toBe('tab:a-0')
    expect(groups[0]?.blocks.map((entry) => entry.key)).toEqual(['tab:a-0', 'tab:a-1'])
  })

  it('returns no group for no blocks', () => {
    expect(groupPluginBlocks([])).toEqual([])
  })
})
