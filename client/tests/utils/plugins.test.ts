/**
 * Tests for `utils/plugins.ts`: the method of a route reference, filling a path template, reading a
 * page's values from its address, resolving a page link, reading a field, the link origin check,
 * value formats, tones, icons and the server-sent event reader, including an abort and a broken
 * connection.
 *
 * @module tests/utils/plugins.test
 */

import { describe, expect, it, vi } from 'vitest'
import { Globe, Puzzle } from 'lucide-vue-next'
import { HttpMethod } from '~/types/httpmethod'
import { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import {
  parsePluginChatResult,
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
  pluginUiTone,
  MAX_PLUGIN_EVENT_CHARS,
  PLUGIN_STREAM_EVENT_TOO_LARGE,
  readPluginEventStream,
  pluginConditionHolds,
  readPluginField,
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
    expect(pluginDocumentLoaded(address, '/client/apps/acme/sites/7')).toBe(expected)
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
