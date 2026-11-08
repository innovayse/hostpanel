/**
 * Tests for the route-parameter guard in `server/utils/requireRouteParam.ts`.
 *
 * A missing or malformed parameter answers a coded 400 and carries no sentence: the BFF authors
 * none. A well-formed one is returned encoded. The plugin slot kinds (`DottedName`, `PluginId`,
 * `PluginWidgetId`) are as narrow as the backend's own rules, and `isPositiveIntKey` is the integer rule
 * the slot query shares.
 *
 * @module tests/server/utils/requireRouteParam.test
 */

import { describe, expect, it, vi } from 'vitest'
import type { H3Event } from 'h3'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { RouteParamKind } from '~/types/routeparamkind'

/** Route parameters the fake event exposes. */
let params: Record<string, string | undefined> = {}

vi.stubGlobal('getRouterParam', (_event: H3Event, name: string) => params[name])
vi.stubGlobal('createError', (options: Record<string, unknown>) => Object.assign(new Error('h3'), options))

const { isPositiveIntKey, requireRouteParam } = await import('~/server/utils/requireRouteParam')

/** An opaque event; the stubbed helper reads {@link params}. */
const event = {} as H3Event

/**
 * Runs the guard and returns what it threw.
 *
 * @param value - The parameter value the fake event carries.
 * @param kind - The kind to validate against; the guard's default when omitted.
 * @returns The thrown error, or undefined when the guard accepted the value.
 */
const refusalOf = (value: string | undefined, kind?: RouteParamKind): Record<string, unknown> | undefined => {
  params = { id: value }
  try {
    requireRouteParam(event, 'id', kind)
  } catch (error) {
    return error as Record<string, unknown>
  }
  return undefined
}

describe('requireRouteParam', () => {
  it('returns an integer parameter by default', () => {
    params = { id: '42' }

    expect(requireRouteParam(event, 'id')).toBe('42')
  })

  it.each([undefined, ''])('refuses %j with a coded 400 and no sentence', (value) => {
    const thrown = refusalOf(value)

    expect(thrown).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
    expect(thrown?.statusMessage).toBeUndefined()
  })

  it.each(['0', '-1', '01', '1.5', '7/../x', '7%2F', '7 ', 'abc', '2147483648', '99999999999', '7?x=1', '..'])(
    'refuses %j as an integer',
    (value) => {
      expect(refusalOf(value)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
    }
  )

  it('accepts the largest 32-bit key', () => {
    params = { id: '2147483647' }

    expect(requireRouteParam(event, 'id', RouteParamKind.Int)).toBe('2147483647')
  })

  it('returns a GUID parameter', () => {
    params = { id: '3f2a1c4e-8b7d-4e6a-9c5b-1d2e3f4a5b6c' }

    expect(requireRouteParam(event, 'id', RouteParamKind.Guid)).toBe('3f2a1c4e-8b7d-4e6a-9c5b-1d2e3f4a5b6c')
  })

  it.each(['abc', '3f2a1c4e8b7d4e6a9c5b1d2e3f4a5b6c', '../y', '3f2a1c4e-8b7d-4e6a-9c5b-1d2e3f4a5b6c/x', '7'])(
    'refuses %j as a GUID',
    (value) => {
      expect(refusalOf(value, RouteParamKind.Guid)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
    }
  )

  it.each(['7', 'pm_1Abc-Z', '3f2a1c4e-8b7d-4e6a-9c5b-1d2e3f4a5b6c'])('returns %j as an identifier', (value) => {
    params = { id: value }

    expect(requireRouteParam(event, 'id', RouteParamKind.Identifier)).toBe(value)
  })

  it.each(['a/b', '../y', 'a b', 'a?b', 'a#b', 'a\\b', 'a%2Fb', 'x'.repeat(129)])('refuses %j as an identifier', (value) => {
    expect(refusalOf(value, RouteParamKind.Identifier)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
  })

  it.each(['client.service.tabs', 'client.dashboard.cards', 'admin.client.tabs'])('returns %j as a dotted name', (value) => {
    params = { id: value }

    expect(requireRouteParam(event, 'id', RouteParamKind.DottedName)).toBe(value)
  })

  it.each(['client', 'Client.x', 'a..b', 'a.b/c', '.a.b', 'a.b.', 'a.b1', '../admin', `${'a'.repeat(33)}.b`])(
    'refuses %j as a dotted name',
    (value) => {
      expect(refusalOf(value, RouteParamKind.DottedName)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
    }
  )

  it.each(['acme', 'acme-sites', 'a1', 'x'.repeat(64)])('returns %j as a plugin id', (value) => {
    params = { id: value }

    expect(requireRouteParam(event, 'id', RouteParamKind.PluginId)).toBe(value)
  })

  it.each(['Acme', 'acme_sites', '-acme', 'acme--sites', 'acme-', 'x'.repeat(65), '..', 'acme/sites', 'ui', 'slots'])('refuses %j as a plugin id', (value) => {
    expect(refusalOf(value, RouteParamKind.PluginId)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
  })

  it.each(['tab', 'site_status', '1-card', 'x'.repeat(64)])('returns %j as a plugin widget id', (value) => {
    params = { id: value }

    expect(requireRouteParam(event, 'id', RouteParamKind.PluginWidgetId)).toBe(value)
  })

  it.each(['_tab', 'Tab', 'a.b', '-tab', 'x'.repeat(65), 'a/b'])('refuses %j as a plugin widget id', (value) => {
    expect(refusalOf(value, RouteParamKind.PluginWidgetId)).toMatchObject({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
  })

  it.each([['1', true], ['2147483647', true], ['0', false], ['04', false], ['2147483648', false], ['-1', false], ['', false], ['1.0', false]])(
    'isPositiveIntKey(%j) is %s',
    (value, expected) => {
      expect(isPositiveIntKey(value)).toBe(expected)
    }
  )
})
