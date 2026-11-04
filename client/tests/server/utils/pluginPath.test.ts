/**
 * Tests for `server/utils/pluginPath.ts`: the plugin route path of the BFF's plugin proxy routes is
 * and the page address of the page lookup are checked and re-encoded segment by segment, so nothing that could leave the plugin's prefix reaches
 * the backend, and the query keeps only string values.
 *
 * @module tests/server/utils/pluginPath.test
 */

import { describe, expect, it, vi } from 'vitest'
import type { H3Event } from 'h3'
import { pluginQuery, requirePluginPagePath, requirePluginPath } from '~/server/utils/pluginPath'

// h3's own `createError` is a Nitro auto-import; the refusal's fields are what is asserted.
vi.stubGlobal('createError', (options: Record<string, unknown>) => Object.assign(new Error(''), options))

/**
 * An event carrying a raw (still encoded) catch-all parameter and a parsed query.
 *
 * @param path - The raw `path` parameter, as h3 hands it with `decode: false`.
 * @param query - The parsed query.
 * @returns The event.
 */
const eventWith = (path: string | undefined, query: Record<string, unknown> = {}): H3Event => {
  vi.stubGlobal('getRouterParam', () => path)
  vi.stubGlobal('getQuery', () => query)
  return {} as H3Event
}

describe('requirePluginPath', () => {
  it('keeps plain segments, and an encoded slash or space inside its own segment', () => {
    expect(requirePluginPath(eventWith('sites/7/chat'))).toBe('sites/7/chat')
    expect(requirePluginPath(eventWith('sites/a%2Fb/chat'))).toBe('sites/a%2Fb/chat')
    expect(requirePluginPath(eventWith('sites/a%20b'))).toBe('sites/a%20b')
  })

  it('re-encodes a segment it decoded, so the backend gets exactly what was checked', () => {
    expect(requirePluginPath(eventWith('sites/caf%C3%A9'))).toBe('sites/caf%C3%A9')
    expect(requirePluginPath(eventWith('sites/a+b'))).toBe('sites/a%2Bb')
  })

  it.each([
    [undefined, 'a missing path'],
    ['', 'an empty path'],
    ['sites//chat', 'an empty segment'],
    ['sites/7/', 'a trailing slash'],
    ['/sites/7', 'a leading slash'],
    ['.', 'a current segment'],
    ['sites/../admin', 'a parent segment'],
    ['../../admin', 'a climb out of the plugin'],
    ['sites/%2E%2E/admin', 'an upper-case encoded parent segment'],
    ['sites/%2e%2e/admin', 'a lower-case encoded parent segment'],
    ['sites/%252e%252e', 'a double encoding'],
    ['sites/a%5Cb', 'a backslash'],
    ['sites/%00', 'a NUL character'],
    ['sites/%0A', 'a line feed'],
    ['sites/%7F', 'a DEL character'],
    ['sites/%E0%A4%A', 'a broken encoding'],
  ])('refuses %j (%s) with a coded 400 and no sentence', (path: string | undefined, _reason: string) => {
    expect(() => requirePluginPath(eventWith(path))).toThrow(expect.objectContaining({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } }))
  })

  it('writes no sentence of its own into the refusal', () => {
    let refusal: Record<string, unknown> = {}
    try {
      requirePluginPath(eventWith('sites/../admin'))
    } catch (error: unknown) {
      refusal = error as Record<string, unknown>
    }

    expect(refusal['statusMessage']).toBeUndefined()
  })
})

describe('requirePluginPagePath', () => {
  it('keeps the leading slash and hands the decoded address on as it is', () => {
    expect(requirePluginPagePath(eventWith(undefined, { path: '/sites/blog' }))).toBe('/sites/blog')
    expect(requirePluginPagePath(eventWith(undefined, { path: '/sites/a b' }))).toBe('/sites/a b')
    expect(requirePluginPagePath(eventWith(undefined, { path: '/կայքեր/сайт' }))).toBe('/կայքեր/сайт')
  })

  it('resolves `/` alone, the plugin\'s root page', () => {
    expect(requirePluginPagePath(eventWith(undefined, { path: '/' }))).toBe('/')
  })

  it.each([
    [undefined, 'a missing address'],
    [['/a', '/b'], 'a repeated key'],
    ['sites/blog', 'no leading slash'],
    ['//', 'an empty segment'],
    ['/sites/../admin', 'a parent segment'],
    ['/sites/%2e', 'a second encoding'],
    ['/sites/a\\b', 'a backslash'],
    [`/${'a'.repeat(1024)}`, 'an overlong address'],
  ])('refuses %s (%s) with a coded 400', (path: unknown, _reason: string) => {
    expect(() => requirePluginPagePath(eventWith(undefined, { path }))).toThrow(expect.objectContaining({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } }))
  })
})

describe('pluginQuery', () => {
  it('passes string values on, repeated keys included, and drops anything else', () => {
    expect(pluginQuery(eventWith('x', { page: '2', tag: ['a', 'b'], odd: { nested: 1 }, count: 3 }))).toBe('?page=2&tag=a&tag=b')
  })

  it('answers an empty string when there is no query', () => {
    expect(pluginQuery(eventWith('x'))).toBe('')
  })

  it('encodes what it passes on, so a value cannot add a key of its own', () => {
    expect(pluginQuery(eventWith('x', { q: 'a&admin=1' }))).toBe('?q=a%26admin%3D1')
  })
})
