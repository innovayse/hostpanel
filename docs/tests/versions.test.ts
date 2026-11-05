/**
 * Unit tests for the version rules of the documentation: the kind of a build, the pages of the latest
 * docs, the canonical address, noindex and the sitemap for each kind of version.
 */
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import {
  canonicalOf,
  existsInLatest,
  hasSitemap,
  isNoindexByVersion,
  kindOf,
  latestLinkOf,
  readLatestPages,
  resolveVersion,
} from '../.vitepress/versions'

/** Pages the latest docs has. */
const PAGES = new Set(['', 'staff/', 'staff/orders'])

/** An older release build whose latest list is known. */
const old = resolveVersion({ version: 'v0.3', latestVersion: 'v0.4', latestUrl: 'https://docs.test/docs/' }, PAGES)
/** The development docs build. */
const next = resolveVersion({ version: 'next', latestVersion: 'v0.4', latestUrl: 'https://docs.test/docs' }, PAGES)
/** The latest release build. */
const latest = resolveVersion({ version: 'v0.4', latestVersion: 'v0.4' }, null)

describe('kindOf', () => {
  it('treats an empty version as the latest', () => {
    expect(kindOf('', '')).toBe('latest')
  })

  it('treats the latest label as the latest and another label as an older release', () => {
    expect(kindOf('v0.4', 'v0.4')).toBe('latest')
    expect(kindOf('v0.3', 'v0.4')).toBe('release')
  })

  it('knows next', () => {
    expect(kindOf('next', 'v0.4')).toBe('next')
  })

  it('rejects a label that is not vMAJOR.MINOR', () => {
    expect(() => kindOf('v0.3.1', 'v0.4')).toThrow(/DOCS_VERSION/)
    expect(() => kindOf('main', '')).toThrow(/DOCS_VERSION/)
  })
})

describe('resolveVersion', () => {
  it('needs the latest address for an older release and for next', () => {
    expect(() => resolveVersion({ version: 'v0.3', latestVersion: 'v0.4' }, null)).toThrow(/DOCS_LATEST_URL/)
    expect(() => resolveVersion({ version: 'next' }, null)).toThrow(/DOCS_LATEST_URL/)
  })

  it('trims the trailing slash of the latest address', () => {
    expect(old.latestUrl).toBe('https://docs.test/docs')
  })

  it('needs no latest address for an unversioned build', () => {
    expect(resolveVersion({}, null)).toMatchObject({ kind: 'latest', label: '', latestUrl: '' })
  })
})

describe('readLatestPages', () => {
  it('reads a JSON array and ignores missing files', () => {
    const dir = mkdtempSync(join(tmpdir(), 'docs-pages-'))
    const file = join(dir, 'pages.json')
    try {
      writeFileSync(file, JSON.stringify(['', 'staff/', 3]))
      expect([...(readLatestPages(file) ?? [])]).toEqual(['', 'staff/'])
      expect(readLatestPages(join(dir, 'missing.json'))).toBeNull()
      expect(readLatestPages('')).toBeNull()
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  })
})

describe('canonicalOf', () => {
  it('points the latest and next at themselves', () => {
    expect(canonicalOf(latest, 'https://docs.test/docs', 'staff/orders')).toBe('https://docs.test/docs/staff/orders')
    expect(canonicalOf(next, 'https://docs.test/docs/next', 'staff/orders')).toBe('https://docs.test/docs/next/staff/orders')
  })

  it('points an older page at the same page in the latest docs when it exists there', () => {
    expect(canonicalOf(old, 'https://docs.test/docs/v0.3', 'staff/orders')).toBe('https://docs.test/docs/staff/orders')
    expect(canonicalOf(old, 'https://docs.test/docs/v0.3', 'staff/')).toBe('https://docs.test/docs/staff/')
    expect(canonicalOf(old, 'https://docs.test/docs/v0.3', '')).toBe('https://docs.test/docs/')
  })

  it('points an older page the latest no longer has at the latest home', () => {
    expect(canonicalOf(old, 'https://docs.test/docs/v0.3', 'staff/removed')).toBe('https://docs.test/docs/')
  })

  it('points every older page at the latest home when the page list is unknown', () => {
    const unknown = resolveVersion({ version: 'v0.3', latestVersion: 'v0.4', latestUrl: 'https://docs.test/docs' }, null)
    expect(canonicalOf(unknown, 'https://docs.test/docs/v0.3', 'staff/orders')).toBe('https://docs.test/docs/')
  })
})

describe('existsInLatest and latestLinkOf', () => {
  it('knows the pages of the latest', () => {
    expect(existsInLatest(old, 'staff/orders')).toBe(true)
    expect(existsInLatest(old, 'gone')).toBe(false)
    expect(existsInLatest(latest, 'anything')).toBe(true)
  })

  it('gives the banner the page in the latest, or its home, and nothing for the latest', () => {
    expect(latestLinkOf(old, 'staff/orders')).toBe('https://docs.test/docs/staff/orders')
    expect(latestLinkOf(old, 'gone')).toBe('https://docs.test/docs/')
    expect(latestLinkOf(next, 'staff/')).toBe('https://docs.test/docs/staff/')
    expect(latestLinkOf(latest, 'staff/')).toBe('')
  })
})

describe('isNoindexByVersion and hasSitemap', () => {
  it('indexes the latest and lists it in the sitemap', () => {
    expect(isNoindexByVersion(latest, 'staff/orders')).toBe(false)
    expect(hasSitemap(latest)).toBe(true)
  })

  it('keeps all of next out of the index and the sitemap', () => {
    expect(isNoindexByVersion(next, 'staff/orders')).toBe(true)
    expect(hasSitemap(next)).toBe(false)
  })

  it('keeps an older page out of the index only when the latest no longer has it, and out of the sitemap always', () => {
    expect(isNoindexByVersion(old, 'staff/orders')).toBe(false)
    expect(isNoindexByVersion(old, 'gone')).toBe(true)
    expect(hasSitemap(old)).toBe(false)
  })
})
