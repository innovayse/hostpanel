/**
 * Tests for `utils/plugins.ts`: the method of a route reference, filling a path template, reading a
 * page's values from its address, resolving a page link, reading a field, the link origin check,
 * value formats, tones, icons and the server-sent event reader, including an abort and a broken
 * connection.
 *
 * @module tests/utils/plugins.test
 */

import { PLUGIN_SELECTION_MESSAGE_TYPE, PLUGIN_SELECTION_MESSAGE_VERSION } from '~/constants/pluginSelection'
import { describe, expect, it, vi } from 'vitest'
import { Globe, Puzzle } from 'lucide-vue-next'
import { HttpMethod } from '~/types/httpmethod'
import { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import {
  parsePluginChatResult,
  pluginActionBody,
  parsePluginChatStep,
  pluginDocumentLoaded,
  pluginFrameAddress,
  fillPluginPath,
  pluginMenuLinks,
  formatPluginValue,
  pluginAlertVariant,
  pluginButtonVariant,
  pluginIcon,
  pluginLinkParams,
  pluginLinkPath,
  pluginLinkTarget,
  pluginPageValues,
  pluginRouteMethod,
  pluginRouteRequest,
  pluginUiTone,
  MAX_PLUGIN_EVENT_CHARS,
  PLUGIN_STREAM_EVENT_TOO_LARGE,
  readPluginEventStream,
  pluginConditionHolds,
  parsePluginSelectionMessage,
  readPluginField,
  warnUnfilledConditionRoute,
} from '~/utils/plugins'
import { UiAlertVariant, UiButtonVariant, UiTone } from '~/utils/ui/enums'

/**
 * A stream that hands out the given chunks one by one.
 *
 * @param chunks - The text chunks.
 * @returns The stream.
 */
const streamOf = (chunks: string[]): ReadableStream<Uint8Array> => {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk))
      }
      controller.close()
    },
  })
}

describe('pluginRouteRequest', () => {
  it('names a route reference by its method and its filled, encoded address, with the query after it', () => {
    expect(pluginRouteRequest({ method: 'get', path: '/sites/{siteId}/state' }, { siteId: 'a/b' }, { page: '2' }))
      .toEqual({ method: 'GET', url: 'sites/a%2Fb/state?page=2' })
    expect(pluginRouteRequest({ method: 'post', path: '/sites/{siteId}' }, { siteId: '7' })).toEqual({ method: 'POST', url: 'sites/7' })
  })

  it('gives two references to the same route the same request, whatever block holds them', () => {
    const one = pluginRouteRequest({ method: 'GET', path: '/sites/{siteId}' }, { siteId: '7' })
    const other = pluginRouteRequest({ method: 'get', path: '/sites/{siteId}' }, { siteId: '7' })

    expect(one).toEqual(other)
  })

  it.each([
    [{ method: 'get', path: '/sites/{siteId}' }, {}, 'a parameter the page does not carry'],
    [{ method: 'head', path: '/sites' }, {}, 'a method the host does not call'],
  ])('is null for %j with %j (%s)', (route, values, _reason: string) => {
    expect(pluginRouteRequest(route, values)).toBeNull()
  })
})

describe('pluginRouteMethod', () => {
  it.each([
    ['get', HttpMethod.Get],
    ['GET', HttpMethod.Get],
    ['post', HttpMethod.Post],
    ['Put', HttpMethod.Put],
    ['patch', HttpMethod.Patch],
    ['delete', HttpMethod.Delete],
  ])('reads %s as %s, ignoring case', (method: string, expected: HttpMethod) => {
    expect(pluginRouteMethod({ method, path: '/x' })).toBe(expected)
  })

  it.each([
    [{ method: 'head', path: '/x' }, 'a method no plugin route may declare'],
    [{ method: 'options', path: '/x' }, 'another such method'],
    [{ method: 'toString', path: '/x' }, 'an inherited name'],
    [{ method: '__proto__', path: '/x' }, 'the prototype key'],
    [{ method: 42, path: '/x' }, 'a number'],
    [undefined, 'no reference'],
  ])('answers nothing for %j (%s)', (route: unknown, _reason: string) => {
    expect(pluginRouteMethod(route as never)).toBeUndefined()
  })
})

describe('fillPluginPath', () => {
  it('fills and encodes every parameter, and answers the path without its leading slash', () => {
    expect(fillPluginPath('/sites/{siteId}/chat', { siteId: 'a b/c' })).toBe('sites/a%20b%2Fc/chat')
    expect(fillPluginPath('/usage', {})).toBe('usage')
    expect(fillPluginPath('/sites/{siteId}/posts/{postId}', { siteId: '7', postId: '9' })).toBe('sites/7/posts/9')
  })

  it('encodes a value that would climb out, so it stays one segment of text', () => {
    expect(fillPluginPath('/sites/{siteId}', { siteId: '../admin' })).toBe('sites/..%2Fadmin')
    expect(fillPluginPath('/sites/{siteId}', { siteId: '%2e%2e' })).toBe('sites/%252e%252e')
  })

  it.each(['.', '..'])('refuses the dot-segment value %s, which encoding leaves as it is', (value) => {
    expect(fillPluginPath('/sites/{siteId}', { siteId: value })).toBeNull()
  })

  it('answers the root template as the empty path', () => {
    expect(fillPluginPath('/', {})).toBe('')
  })

  it.each([
    '/sites/../../account',
    '/sites/./x',
    '/../other-plugin/x',
    '/sites//x',
    '/sites/%2e%2e/x',
    '/sites/x?y=1',
    '/sites/x#y',
    '/sites/a\\b',
    '/sites/a\u0000b',
    '/sites/a\u007fb',
  ])('refuses the unsafe literal segment in %j', (template) => {
    expect(fillPluginPath(template, {})).toBeNull()
  })

  it('refuses a path with a missing or empty parameter', () => {
    expect(fillPluginPath('/sites/{siteId}/chat', {})).toBeNull()
    expect(fillPluginPath('/sites/{siteId}', { siteId: '' })).toBeNull()
  })

  it('never reads an inherited name as a value', () => {
    expect(fillPluginPath('/x/{toString}', {})).toBeNull()
  })
})

describe('pluginPageValues', () => {
  it('reads each parameter of the template from its segment of the address', () => {
    expect(pluginPageValues('/sites/{siteId}/posts/{postId}', ['sites', 'blog', 'posts', '9'])).toEqual({ siteId: 'blog', postId: '9' })
    expect(pluginPageValues('/sites', ['sites'])).toEqual({})
  })

  it('reads nothing when the address does not have the template\'s shape', () => {
    expect(pluginPageValues('/sites/{siteId}', ['sites'])).toEqual({})
    expect(pluginPageValues('/sites/{siteId}', ['sites', '7', 'extra'])).toEqual({})
  })
})

describe('pluginLinkPath', () => {
  const pages = [
    { pluginId: 'acme-sites', id: 'site', path: '/sites/{siteId}', title: 'Site' },
    { pluginId: 'acme-mail', id: 'site', path: '/elsewhere/{siteId}', title: 'Other' },
  ]

  it('fills the linked page\'s template with the link\'s values', () => {
    expect(pluginLinkPath(pages, 'acme-sites', 'site', { siteId: '7' })).toBe('sites/7')
  })

  it('opens nothing for a page whose path would leave the plugin', () => {
    const climbing = [{ pluginId: 'acme-sites', id: 'up', path: '/../account/{siteId}', title: 'Up' }]

    expect(pluginLinkPath(climbing, 'acme-sites', 'up', { siteId: '7' })).toBeNull()
  })

  it('opens nothing for a page another plugin owns, a page not in the index or a missing value', () => {
    expect(pluginLinkPath(pages, 'acme-sites', 'other', { siteId: '7' })).toBeNull()
    expect(pluginLinkPath(pages, 'nobody', 'site', { siteId: '7' })).toBeNull()
    expect(pluginLinkPath(pages, 'acme-sites', 'site', {})).toBeNull()
  })
})

describe('readPluginField and pluginLinkParams', () => {
  it('reads a dotted path, the whole reply without one, and nothing through a missing key', () => {
    const reply = { site: { previewUrl: 'https://a.example' } }
    expect(readPluginField(reply, 'site.previewUrl')).toBe('https://a.example')
    expect(readPluginField(reply, undefined)).toBe(reply)
    expect(readPluginField(reply, 'site.missing.deeper')).toBeUndefined()
    expect(readPluginField('text', 'length')).toBeUndefined()
    expect(readPluginField({}, 'constructor')).toBeUndefined()
  })

  it('reads a link\'s parameters by name, and none when one is missing or empty', () => {
    expect(pluginLinkParams({ page: 'site', params: { siteId: 'id', tab: 'tab' } }, { id: 7, tab: 'log' })).toEqual({ siteId: '7', tab: 'log' })
    expect(pluginLinkParams({ page: 'site', params: { siteId: 'id' } }, {})).toBeNull()
    expect(pluginLinkParams({ page: 'site', params: { siteId: 'id' } }, { id: '' })).toBeNull()
    expect(pluginLinkParams({ page: 'sites' }, null)).toEqual({})
  })
})

describe('pluginConditionHolds', () => {
  /** The route every condition below reads. */
  const route = { method: 'get', path: '/sites/{siteId}' }

  it('holds when the field equals the one value or one of the list', () => {
    expect(pluginConditionHolds({ route, field: 'site.status', equals: 'live' }, { site: { status: 'live' } })).toBe(true)
    expect(pluginConditionHolds({ route, field: 'site.status', equals: ['ready', 'live'] }, { site: { status: 'ready' } })).toBe(true)
    expect(pluginConditionHolds({ route, field: 'exists', equals: true }, { exists: true })).toBe(true)
    expect(pluginConditionHolds({ route, field: 'site', equals: null }, { site: null })).toBe(true)
  })

  it('does not hold for another value, an absent field or a value of another type', () => {
    expect(pluginConditionHolds({ route, field: 'site.status', equals: ['ready', 'live'] }, { site: { status: 'building' } })).toBe(false)
    expect(pluginConditionHolds({ route, field: 'site.status', equals: null }, { site: {} })).toBe(false)
    expect(pluginConditionHolds({ route, field: 'site.status', equals: null }, null)).toBe(false)
    expect(pluginConditionHolds({ route, field: 'version', equals: '1' }, { version: 1 })).toBe(false)
    expect(pluginConditionHolds({ route, field: 'version', equals: 1 }, { version: '1' })).toBe(false)
    expect(pluginConditionHolds({ route, field: 'exists', equals: true }, { exists: 'true' })).toBe(false)
  })
})

describe('warnUnfilledConditionRoute', () => {
  it('warns in development, naming the route and the parameters the page has', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    warnUnfilledConditionRoute('/sites/{siteId}/versions/{versionId}', { siteId: '7' }, true)

    expect(warn).toHaveBeenCalledOnce()
    expect(String(warn.mock.calls[0]?.[0])).toContain('/sites/{siteId}/versions/{versionId}')
    expect(String(warn.mock.calls[0]?.[0])).toContain('siteId')
    warn.mockRestore()
  })

  it('stays silent outside development, and the card still drops the action', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const visibleWhen = { route: { method: 'get', path: '/sites/{siteId}/versions/{versionId}' }, field: 'live', equalsAny: [true] }

    const card = parsePluginChatResult({ title: 'Ready', actions: [{ label: 'Open', href: '/client/dashboard', visibleWhen }] }, { siteId: '7' }, [])

    expect(card?.actions).toEqual([])
    expect(warn).not.toHaveBeenCalled()
    warn.mockRestore()
  })
})

describe('pluginLinkTarget', () => {
  const origins = ['https://*.sites.example.test', 'https://docs.example.test']

  it.each([
    'https://blog.sites.example.test/preview',
    'https://a.b.sites.example.test/',
    'https://docs.example.test/guide',
  ])('admits %s as an external link', (href) => {
    expect(pluginLinkTarget(href, origins)).toEqual({ href, external: true })
  })

  it('admits a host-relative address as an internal link, a path that only starts like the API included', () => {
    expect(pluginLinkTarget('/client/dashboard', origins)).toEqual({ href: '/client/dashboard', external: false })
    expect(pluginLinkTarget('/apis-guide', origins)).toEqual({ href: '/apis-guide', external: false })
  })

  it.each([
    ['javascript:alert(1)', 'a script'],
    ['data:text/html,<script>alert(1)</script>', 'a data address'],
    ['http://blog.sites.example.test/', 'plain http'],
    ['https://user@docs.example.test/', 'user info'],
    ['https://sites.example.test/', 'the bare domain of a wildcard'],
    ['https://evil.test/?https://x.sites.example.test', 'another origin'],
    ['https://sites.example.test.evil.test/', 'a suffix trick'],
    ['https://docs.example.test:8443/', 'another port'],
    ['//evil.test/', 'a protocol-relative address'],
    ['/api/portal/client/me', 'the API under the portal'],
    ['/api', 'the API root'],
    ['/API/portal/client/me', 'the API in another case'],
    ['/client/../api/x', 'the API behind a parent segment'],
    ['/%61pi/x', 'the API behind an encoded letter'],
    ['/%E0%A4%A', 'a path that cannot be decoded'],
    ['/client\\dashboard', 'a backslash'],
    ['/client/\u0000', 'a control character'],
    ['https://user:pw@docs.example.test/', 'user info with a password'],
    ['sites/7', 'a relative path without a slash'],
    ['', 'an empty address'],
    [42, 'a number'],
  ])('refuses %s (%s)', (href: unknown, _reason: string) => {
    expect(pluginLinkTarget(href, origins)).toBeNull()
  })

  it('refuses every external address when the plugin allows no origin', () => {
    expect(pluginLinkTarget('https://blog.sites.example.test/', [])).toBeNull()
  })
})

describe('formatPluginValue', () => {
  it('writes text as text, objects as JSON and missing values as a dash', () => {
    expect(formatPluginValue('<b>bold</b>', undefined, 'en')).toBe('<b>bold</b>')
    expect(formatPluginValue(3, PluginValueFormat.Text, 'en')).toBe('3')
    expect(formatPluginValue({ a: 1 }, undefined, 'en')).toBe('{"a":1}')
    expect(formatPluginValue(null, PluginValueFormat.DateTime, 'en')).toBe('—')
  })

  it('formats money in the reader\'s locale and a date with the host formatter', () => {
    expect(formatPluginValue({ amount: 12.5, currency: 'USD' }, PluginValueFormat.Money, 'en')).toBe('$12.50')
    expect(formatPluginValue('2026-10-30T12:00:00Z', PluginValueFormat.DateTime, 'en')).toMatch(/2026/)
  })
})

describe('tones and icons', () => {
  it('maps plugin tones to the kit\'s, neutral by default', () => {
    expect(pluginUiTone(PluginBlockTone.Danger)).toBe(UiTone.Danger)
    expect(pluginUiTone(undefined)).toBe(UiTone.Default)
    expect(pluginAlertVariant(PluginBlockTone.Danger)).toBe(UiAlertVariant.Error)
    expect(pluginAlertVariant(undefined)).toBe(UiAlertVariant.Info)
    expect(pluginButtonVariant(PluginBlockTone.Primary)).toBe(UiButtonVariant.Primary)
    expect(pluginButtonVariant(undefined)).toBe(UiButtonVariant.Outline)
  })

  it('draws a known icon, and the default one for an unknown, inherited or missing name', () => {
    expect(pluginIcon('globe')).toBe(Globe)
    expect(pluginIcon('toString')).toBe(Puzzle)
    expect(pluginIcon(null)).toBe(Puzzle)
  })
})

describe('readPluginEventStream', () => {
  it('hands over events split across chunks, joins data lines and skips heartbeats', async () => {
    const events: PluginStreamEvent[] = []

    await readPluginEventStream(streamOf([
      ': heartbeat\n\n',
      'event: delta\ndata: {"text":"Hel',
      'lo"}\n\nevent: status\r\ndata: building\r\n\r\n',
      'data: line one\ndata: line two\n\n',
      'event: done\ndata: {}\n\n',
    ]), event => events.push(event))

    expect(events).toEqual([
      { type: 'delta', data: { text: 'Hello' } },
      { type: 'status', data: 'building' },
      { type: 'message', data: 'line one\nline two' },
      { type: 'done', data: {} },
    ])
  })

  it('reads a CRLF split across two chunks as one line end, not two', async () => {
    const events: PluginStreamEvent[] = []

    await readPluginEventStream(streamOf([
      'event: delta\r',
      '\ndata: {"text":"a"}\r',
      '\n\r',
      '\nevent: done\r\ndata: {}\r\n\r\n',
    ]), event => events.push(event))

    expect(events).toEqual([
      { type: 'delta', data: { text: 'a' } },
      { type: 'done', data: {} },
    ])
  })

  it('stops at once on abort, hands over nothing after it and releases the stream', async () => {
    const controller = new AbortController()
    const encoder = new TextEncoder()
    const cancel = vi.fn()
    const stream = new ReadableStream<Uint8Array>({
      start(source) {
        source.enqueue(encoder.encode('event: delta\ndata: {"text":"a"}\n\n'))
      },
      cancel,
    })
    const events: PluginStreamEvent[] = []

    const reading = readPluginEventStream(stream, (event) => {
      events.push(event)
      controller.abort()
    }, controller.signal)

    await expect(reading).resolves.toBeUndefined()
    expect(events).toHaveLength(1)
    expect(cancel).toHaveBeenCalled()
    expect(stream.locked).toBe(false)
  })

  it('throws when the connection breaks, after handing over what arrived', async () => {
    const encoder = new TextEncoder()
    let pull = 0
    const stream = new ReadableStream<Uint8Array>({
      pull(source) {
        pull += 1
        if (pull === 1) {
          source.enqueue(encoder.encode('event: delta\ndata: {"text":"a"}\n\n'))
        } else {
          source.error(new TypeError('network error'))
        }
      },
    })
    const events: PluginStreamEvent[] = []

    await expect(readPluginEventStream(stream, event => events.push(event))).rejects.toThrow('network error')
    expect(events).toEqual([{ type: 'delta', data: { text: 'a' } }])
  })
  it('finds an event end that falls across two chunks', async () => {
    const events: PluginStreamEvent[] = []

    await readPluginEventStream(streamOf(['event: delta\ndata: {"text":"a"}\n', '\nevent: done\ndata: {}\n\n']), event => events.push(event))

    expect(events).toEqual([{ type: 'delta', data: { text: 'a' } }, { type: 'done', data: {} }])
  })

  it('gives up an event that passes the size cap: the stream is cancelled and a coded failure thrown', async () => {
    const encoder = new TextEncoder()
    const cancel = vi.fn()
    const chunk = encoder.encode(`data: ${'x'.repeat(64 * 1024)}`)
    let sent = 0
    const stream = new ReadableStream<Uint8Array>({
      pull(source) {
        if (sent === 0) {
          source.enqueue(encoder.encode('event: delta\ndata: {"text":"a"}\n\n'))
        } else {
          source.enqueue(chunk)
        }
        sent += 1
      },
      cancel,
    })
    const events: PluginStreamEvent[] = []

    await expect(readPluginEventStream(stream, event => events.push(event)))
      .rejects.toMatchObject({ statusCode: 502, data: { code: PLUGIN_STREAM_EVENT_TOO_LARGE } })
    expect(events).toEqual([{ type: 'delta', data: { text: 'a' } }])
    expect(cancel).toHaveBeenCalled()
    // Stopped soon after the cap, not after reading on: one chunk past it at most.
    expect(sent * chunk.length).toBeLessThan(MAX_PLUGIN_EVENT_CHARS + 3 * chunk.length)
    expect(stream.locked).toBe(false)
  })
})

describe('pluginMenuLinks', () => {
  it('links the menu items by order, leaving out an unknown page and a page that takes parameters', () => {
    const pages = [
      { pluginId: 'acme', id: 'sites', path: '/sites', title: 'Sites' },
      { pluginId: 'acme', id: 'site', path: '/sites/{siteId}', title: 'Site' },
      { pluginId: 'beta', id: 'home', path: '/home', title: 'Home' },
    ]
    const menus = [
      { pluginId: 'beta', id: 'home', label: 'Beta', page: 'home', icon: null, order: 2 },
      { pluginId: 'acme', id: 'sites', label: 'Sites', page: 'sites', icon: 'globe', order: 1 },
      { pluginId: 'acme', id: 'site', label: 'Site', page: 'site', icon: null, order: 0 },
      { pluginId: 'acme', id: 'gone', label: 'Gone', page: 'gone', icon: null, order: 0 },
    ]

    expect(pluginMenuLinks(menus, pages)).toEqual([
      { label: 'Sites', icon: 'globe', url: '/client/apps/acme/sites' },
      { label: 'Beta', icon: null, url: '/client/apps/beta/home' },
    ])
  })

  it.each(['/../account', '/sites/..', '/a//b', '/x%2e', '/x?y', '/x#y'])('leaves out a page whose path %j would leave the plugin', (path) => {
    const pages = [{ pluginId: 'acme', id: 'p', path, title: 'P' }]
    const menus = [{ pluginId: 'acme', id: 'p', label: 'P', page: 'p', icon: null, order: 0 }]

    expect(pluginMenuLinks(menus, pages)).toEqual([])
  })
})

describe('pluginFrameAddress', () => {
  /** The allowed origin. */
  const origins = ['https://a.example']

  it('answers the normalised address on an exact origin', () => {
    expect(pluginFrameAddress('https://A.example/p', origins, 'https://panel.test')).toBe('https://a.example/p')
  })

  it.each([
    'https://evil.test/',
    'https://b.a.example/',
    'http://a.example/',
    'javascript:alert(1)',
    'data:text/html,x',
    'https://u:p@a.example/',
    'not a url',
    5,
    null,
  ])('refuses %s', (value) => {
    expect(pluginFrameAddress(value, origins, 'https://panel.test')).toBeNull()
  })

  it('refuses the panel\'s own origin even when listed', () => {
    expect(pluginFrameAddress('https://a.example/', origins, 'https://a.example')).toBeNull()
  })
})

describe('pluginDocumentLoaded', () => {
  it.each([
    ['https://p.test/client/apps/acme/sites/7', true],
    ['https://p.test/client/apps/acme/sites/7/', true],
    ['https://p.test/client/apps/acme/sites/7?a=1', true],
    ['https://p.test/client/apps/acme/overview', false],
    ['https://p.test/client/apps/acme', false],
    ['https://p.test/client/apps/acme-two/sites/7', false],
    ['https://p.test/client/dashboard', false],
    ['nonsense', false],
    [undefined, false],
  ])('for %s answers %s', (address, expected) => {
    expect(pluginDocumentLoaded({ name: address, serverTiming: [{ name: 'plugin-frames' }] }, '/client/apps/acme/sites/7')).toBe(expected)
  })

  it('refuses a document whose server wrote no origins: one opened signed out stays closed after a sign-in inside it', () => {
    const address = 'https://p.test/client/apps/acme/sites/7'

    expect(pluginDocumentLoaded({ name: address, serverTiming: [] }, '/client/apps/acme/sites/7')).toBe(false)
    expect(pluginDocumentLoaded({ name: address }, '/client/apps/acme/sites/7')).toBe(false)
    expect(pluginDocumentLoaded(undefined, '/client/apps/acme/sites/7')).toBe(false)
  })

  it('compares a language-prefixed page with its own address', () => {
    expect(pluginDocumentLoaded({ name: 'https://p.test/hy/client/apps/acme/sites/7', serverTiming: [{ name: 'plugin-frames' }] }, '/hy/client/apps/acme/sites/7')).toBe(true)
  })
})

describe('parsePluginChatStep', () => {
  it('keys a step by its id, else its label', () => {
    expect(parsePluginChatStep({ id: 'a', label: 'L', state: 'running' })?.key).toBe('a')
    expect(parsePluginChatStep({ label: 'L', state: 'done' })?.key).toBe('L')
  })

  it.each([[{ label: 'L', state: 'stopped' }], [{ label: 'L', state: 'odd' }], [{ state: 'done' }], [{ id: 1, label: 'L', state: 'done' }], ['text']])('ignores %j', (data) => {
    expect(parsePluginChatStep(data)).toBeNull()
  })
})

describe('parsePluginChatResult', () => {
  it('drops actions past the fifth and any action with both or neither of route and href', () => {
    const link = { label: 'L', href: '/client/x' }
    const result = parsePluginChatResult({
      text: 'T',
      actions: [link, { label: 'Both', href: '/client/x', route: { method: 'post', path: '/x' } }, { label: 'Neither' }, link, link, link],
    }, {}, [])

    expect(result?.actions.map(action => action.key)).toEqual([0, 3, 4])
  })

  it('refuses actions that are not a list', () => {
    expect(parsePluginChatResult({ text: 'T', actions: 'x' }, {}, [])).toBeNull()
  })
})

describe('parsePluginChatResult until', () => {
  /** A card whose action starts work that runs on. */
  const card = (until: unknown) => ({ title: 'T', actions: [{ label: 'Undo', route: { method: 'post', path: '/undo' }, until }] })

  it('keeps the chat blocks reading until the condition holds', () => {
    const until = { route: { method: 'get', path: '/state' }, field: 'state', equalsAny: ['done'] }

    const block = parsePluginChatResult(card(until), {}, [], ['a'])?.actions[0]?.block
    expect(block?.followUp).toEqual({ refresh: ['a'], until: { route: until.route, field: 'state', equals: ['done'] } })
  })

  it('starts no follow-up for a malformed condition', () => {
    expect(parsePluginChatResult(card({ field: 'state' }), {}, [], ['a'])?.actions[0]?.block?.followUp).toBeUndefined()
  })
})

describe('parsePluginChatResult visibleWhen', () => {
  /** A condition as an event spells it: `equalsAny`, since a C# record cannot have a member named `equals`. */
  const when = { route: { method: 'get', path: '/state/{siteId}' }, field: 'publishVersion', equalsAny: [5] }

  /** A card with one action of each kind, each carrying the given condition. */
  const card = (visibleWhen: unknown) => ({
    title: 'T',
    actions: [
      { label: 'Publish', route: { method: 'post', path: '/publish' }, visibleWhen },
      { label: 'Open', href: '/client/x', visibleWhen },
      { label: 'Try again', resend: true, visibleWhen },
    ],
  })

  it('keeps a condition on every kind of action, as the view reads one', () => {
    const actions = parsePluginChatResult(card(when), { siteId: '7' }, [])?.actions

    const expected = { route: when.route, field: 'publishVersion', equals: [5] }
    expect(actions?.map(action => action.visibleWhen)).toEqual([expected, expected, expected])
  })

  it('leaves an action with no condition always shown', () => {
    const actions = parsePluginChatResult(card(undefined), {}, [])?.actions

    expect(actions).toHaveLength(3)
    expect(actions?.some(action => 'visibleWhen' in action)).toBe(false)
  })

  it.each([
    ['null', null],
    ['a string', 'x'],
    ['a route that is not a GET', { ...when, route: { method: 'post', path: '/state' } }],
    ['a route the page cannot fill', { ...when, route: { method: 'get', path: '/state/{missing}' } }],
    ['no route', { field: 'publishVersion', equalsAny: [5] }],
    ['a field with a bad character', { ...when, field: 'a b' }],
    ['a field over the bound', { ...when, field: 'a'.repeat(201) }],
    ['no field', { route: when.route, equalsAny: [5] }],
    ['no values', { ...when, equalsAny: [] }],
    ['values that are not a list', { ...when, equalsAny: 5 }],
    ['too many values', { ...when, equalsAny: Array.from({ length: 21 }, (_, index) => index) }],
    ['a value that is no scalar', { ...when, equalsAny: [{ a: 1 }] }],
    ['a value that is not finite', { ...when, equalsAny: [Number.POSITIVE_INFINITY] }],
    ['the C# spelling used for the view document', { route: when.route, field: 'publishVersion', equals: [5] }],
  ])('drops the action whose condition is %s, never showing what it meant to hide', (_name, visibleWhen) => {
    const result = parsePluginChatResult(card(visibleWhen), { siteId: '7' }, [])

    expect(result?.actions).toEqual([])
  })

  it('accepts a single value, a string, a boolean and null among the values', () => {
    const values = [5, 'live', true, null]
    const actions = parsePluginChatResult(card({ ...when, equalsAny: values }), { siteId: '7' }, [])?.actions

    expect(actions?.[0]?.visibleWhen?.equals).toEqual(values)
  })
})

describe('action body from page data', () => {
  /** A view's body source reading one member. */
  const source = { route: { method: 'get', path: '/state' }, fields: { expectedVersion: 'publish.version' } }

  it('builds the body from the reply', () => {
    expect(pluginActionBody(source, { publish: { version: 14 } })).toEqual({ expectedVersion: 14 })
  })

  it.each([[undefined], [{}], [{ publish: { version: null } }], [{ publish: { version: { a: 1 } } }], [{ publish: { version: Number.NaN } }]])(
    'refuses to build a partial body from %j', (reply) => {
      expect(pluginActionBody(source, reply)).toBeNull()
    })

  /** A result event with one action carrying the given body. */
  const card = (body: unknown): unknown => ({ title: 'T', actions: [{ label: 'Publish', route: { method: 'post', path: '/publish' }, body }] })

  it('keeps the literal body of a result card\'s action', () => {
    expect(parsePluginChatResult(card({ expectedVersion: 4 }), {}, [])?.actions[0]?.block?.body).toEqual({ expectedVersion: 4 })
  })

  it.each([[null], [[1]], ['x'], [{ __proto__x: 1 }], [{ a: { b: 1 } }], [Object.fromEntries(Array.from({ length: 11 }, (_, index) => [`a${index}`, 1]))]])(
    'drops a result card\'s action whose body is %j', (body) => {
      expect(parsePluginChatResult(card(body), {}, [])?.actions).toEqual([])
    })
})

describe('parsePluginSelectionMessage', () => {
  /** A well-formed toggle. */
  const toggle = { type: PLUGIN_SELECTION_MESSAGE_TYPE, version: PLUGIN_SELECTION_MESSAGE_VERSION, release: 'r4', event: 'toggle', id: 'pages.0.title' }

  it('takes the exact shapes', () => {
    expect(parsePluginSelectionMessage(toggle, 'r4')).toEqual({ event: 'toggle', id: 'pages.0.title' })
    expect(parsePluginSelectionMessage({ ...toggle, event: 'ready', id: undefined, ...{} }, 'r4')).toBeNull()
    const { id: _id, ...ready } = toggle
    expect(parsePluginSelectionMessage({ ...ready, event: 'ready' }, 'r4')).toEqual({ event: 'ready' })
    expect(parsePluginSelectionMessage({ ...ready, event: 'exit' }, 'r4')).toEqual({ event: 'exit' })
  })

  it.each([
    ['another type', { ...toggle, type: 'other' }],
    ['another version', { ...toggle, version: 2 }],
    ['a string version', { ...toggle, version: '1' }],
    ['another release', { ...toggle, release: 'r5' }],
    ['an extra member', { ...toggle, text: 'planted' }],
    ['an id with markup', { ...toggle, id: '<img src=x>' }],
    ['an id with a newline', { ...toggle, id: 'name\n' }],
    ['an id over the bound', { ...toggle, id: `a.${'b'.repeat(200)}` }],
    ['an unknown event', { ...toggle, event: 'navigate' }],
    ['an array', [toggle]],
    ['a string', 'toggle'],
    ['null', null],
  ])('refuses %s', (_name, data) => {
    expect(parsePluginSelectionMessage(data, 'r4')).toBeNull()
  })

  it('refuses everything while the release is unknown', () => {
    expect(parsePluginSelectionMessage({ ...toggle, release: '' }, '')).toBeNull()
  })
})
