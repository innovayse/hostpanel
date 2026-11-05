/**
 * Unit tests for `server/utils/documentNavigation.ts`: a browser opening a page counts, and nothing else does.
 *
 * @module tests/server/utils/documentNavigation.test
 */

import { describe, expect, it } from 'vitest'
import { makeEvent } from '../../helpers/bffHarness'
import { isDocumentNavigation } from '~/server/utils/documentNavigation'
import { HttpMethod } from '~/types/httpmethod'

/** A browser's user agent. */
const BROWSER = 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36'

describe('isDocumentNavigation', () => {
  it('accepts a GET the browser marks as a document', () => {
    const event = makeEvent({ headers: { 'user-agent': BROWSER, 'sec-fetch-dest': 'document' } })

    expect(isDocumentNavigation(event)).toBe(true)
  })

  it('believes the fetch mark over the Accept header', () => {
    const event = makeEvent({ headers: { 'user-agent': BROWSER, 'sec-fetch-dest': 'iframe', accept: 'text/html' } })

    expect(isDocumentNavigation(event)).toBe(false)
  })

  it('falls back to an Accept header asking for HTML', () => {
    expect(isDocumentNavigation(makeEvent({ headers: { 'user-agent': BROWSER, accept: 'text/html,*/*' } }))).toBe(true)
    expect(isDocumentNavigation(makeEvent({ headers: { 'user-agent': BROWSER, accept: 'application/json' } }))).toBe(false)
  })

  it('refuses another method and a known bot', () => {
    const headers = { 'sec-fetch-dest': 'document', 'user-agent': BROWSER }

    expect(isDocumentNavigation(makeEvent({ headers, method: HttpMethod.Post }))).toBe(false)
    expect(isDocumentNavigation(makeEvent({ headers: { ...headers, 'user-agent': 'Googlebot/2.1' } }))).toBe(false)
  })
})
