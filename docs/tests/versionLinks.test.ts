/**
 * Unit tests for the links of the version switcher: the root of the docs, which links of `versions.json`
 * are accepted, and the fall back to the home page of a version when the page is missing there.
 */
import { describe, expect, it } from 'vitest'
import { docsRootOf, hrefFor, pageExists, pageIn, parseVersions, safeVersionLink } from '../.vitepress/theme/versionLinks'
import type { Probe, ProbeResponse, VersionEntry } from '../.vitepress/theme/versionLinks'

/** The entry of an older release below `/docs/`. */
const release: VersionEntry = { label: 'v0.3', link: '/docs/v0.3/', kind: 'release' }

describe('docsRootOf', () => {
  it('is the base itself for the latest docs', () => {
    expect(docsRootOf('/docs/', 'latest', 'v0.4')).toBe('/docs/')
    expect(docsRootOf('/docs/', 'latest', '')).toBe('/docs/')
  })

  it('cuts the version off the base of an older release and of next', () => {
    expect(docsRootOf('/docs/v0.3/', 'release', 'v0.3')).toBe('/docs/')
    expect(docsRootOf('/docs/next/', 'next', 'next')).toBe('/docs/')
  })

  it('handles a docs site served at the root of a domain', () => {
    expect(docsRootOf('/', 'latest', '')).toBe('/')
    expect(docsRootOf('/v0.3/', 'release', 'v0.3')).toBe('/')
  })

  it('normalizes a base without slashes and leaves a base that does not end with the label', () => {
    expect(docsRootOf('docs/v0.3', 'release', 'v0.3')).toBe('/docs/')
    expect(docsRootOf('/docs/', 'release', 'v0.3')).toBe('/docs/')
  })

  it('does not cut a segment that only ends like the label', () => {
    expect(docsRootOf('/docs/xv0.3/', 'release', 'v0.3')).toBe('/docs/xv0.3/')
  })
})

describe('safeVersionLink', () => {
  it('accepts a path below the root and returns it with a trailing slash', () => {
    expect(safeVersionLink('/docs/', '/docs/')).toBe('/docs/')
    expect(safeVersionLink('/docs/v0.3/', '/docs/')).toBe('/docs/v0.3/')
    expect(safeVersionLink('/docs/v0.3', '/docs/')).toBe('/docs/v0.3/')
  })

  it('accepts every path when the docs are at the root of the domain, but not another site', () => {
    expect(safeVersionLink('/v0.3/', '/')).toBe('/v0.3/')
    expect(safeVersionLink('//evil.example/', '/')).toBeNull()
    expect(safeVersionLink('https://evil.example/', '/')).toBeNull()
  })

  it.each([
    ['an absolute address', 'https://evil.example/docs/'],
    ['an address without a scheme', '//evil.example/docs/'],
    ['a scheme without slashes', 'javascript:alert(1)'],
    ['a data address', 'data:text/html,x'],
    ['a relative path', 'docs/v0.3/'],
    ['a backslash path', '/\\evil.example/'],
    ['a slash and a backslash', '/\\/evil.example/'],
    ['a path outside the root', '/other/v0.3/'],
    ['a path that only starts like the root', '/docsevil/'],
    ['a path that climbs out', '/docs/../admin/'],
    ['an encoded climb', '/docs/%2e%2e/admin/'],
    ['a path with a space', '/docs/v 0.3/'],
    ['a path with a tab', '/docs/v0.3/\t'],
    ['a path with a newline', '/docs/\nevil/'],
    ['an empty string', ''],
  ])('refuses %s', (_name, link) => {
    expect(safeVersionLink(link, '/docs/')).toBeNull()
  })

  it.each([[undefined], [null], [42], [{}], [['/docs/']]])('refuses a value that is not a string (%j)', (value) => {
    expect(safeVersionLink(value, '/docs/')).toBeNull()
  })

  it('drops the query and the fragment', () => {
    expect(safeVersionLink('/docs/v0.3/?x=1#top', '/docs/')).toBe('/docs/v0.3/')
  })
})

describe('parseVersions', () => {
  it('keeps well formed entries in file order', () => {
    const body = {
      versions: [
        { label: 'v0.4', link: '/docs/', kind: 'latest' },
        { label: 'v0.3', link: '/docs/v0.3/', kind: 'release' },
        { label: 'next', link: '/docs/next/', kind: 'next' },
      ],
    }

    expect(parseVersions(body, '/docs/').map((entry) => entry.label)).toEqual(['v0.4', 'v0.3', 'next'])
  })

  it('drops an entry whose link leaves the docs and keeps the others', () => {
    const body = {
      versions: [
        { label: 'v0.4', link: '/docs/', kind: 'latest' },
        { label: 'evil', link: 'https://evil.example/', kind: 'release' },
        { label: 'v0.3', link: '/docs/v0.3/', kind: 'release' },
      ],
    }

    expect(parseVersions(body, '/docs/').map((entry) => entry.label)).toEqual(['v0.4', 'v0.3'])
  })

  it('drops an entry with a missing label, a blank label or an unknown kind', () => {
    const body = {
      versions: [
        { link: '/docs/v0.1/', kind: 'release' },
        { label: '  ', link: '/docs/v0.2/', kind: 'release' },
        { label: 'v0.3', link: '/docs/v0.3/', kind: 'beta' },
        { label: 'v0.4', link: '/docs/v0.4/' },
        { label: 'v0.5', link: '/docs/v0.5/', kind: 'release' },
      ],
    }

    expect(parseVersions(body, '/docs/').map((entry) => entry.label)).toEqual(['v0.5'])
  })

  it('ignores entries that are not objects', () => {
    expect(parseVersions({ versions: [null, 'x', 3, ['/docs/']] }, '/docs/')).toEqual([])
  })

  it.each([[undefined], [null], ['text'], [{}], [{ versions: 'x' }], [{ versions: {} }]])('gives no list for a file that is %j', (body) => {
    expect(parseVersions(body, '/docs/')).toEqual([])
  })
})

describe('pageIn and hrefFor', () => {
  it('joins the path of the version and the path of the page', () => {
    expect(pageIn(release, 'operators/install')).toBe('/docs/v0.3/operators/install')
    expect(pageIn(release, '')).toBe('/docs/v0.3/')
  })

  it('links to the same page when it exists and to the home of the version when it does not', () => {
    expect(hrefFor(release, 'operators/install', true)).toBe('/docs/v0.3/operators/install')
    expect(hrefFor(release, 'operators/install', false)).toBe('/docs/v0.3/')
  })
})

/** A response with a status. */
const answer = (status: number): ProbeResponse => ({ ok: status >= 200 && status < 300, status })

describe('pageExists', () => {
  it('is true for a 2xx answer to HEAD', async () => {
    const methods: string[] = []
    const probe: Probe = async (_url, init) => {
      methods.push(init.method)
      return answer(200)
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(true)
    expect(methods).toEqual(['HEAD'])
  })

  it('is false for a 404', async () => {
    expect(await pageExists('/docs/v0.3/a', async () => answer(404))).toBe(false)
  })

  it('is false for a server error and for a redirect that was not followed', async () => {
    expect(await pageExists('/docs/v0.3/a', async () => answer(500))).toBe(false)
    expect(await pageExists('/docs/v0.3/a', async () => answer(302))).toBe(false)
  })

  it('asks again with GET when HEAD is not allowed', async () => {
    const methods: string[] = []
    const probe: Probe = async (_url, init) => {
      methods.push(init.method)
      return init.method === 'HEAD' ? answer(405) : answer(200)
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(true)
    expect(methods).toEqual(['HEAD', 'GET'])
  })

  it('is false when the request fails', async () => {
    const probe: Probe = async () => {
      throw new TypeError('network error')
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(false)
  })

  it('is false when no answer comes in time', async () => {
    const probe: Probe = (_url, init) =>
      new Promise((_resolve, reject) => {
        init.signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))
      })

    expect(await pageExists('/docs/v0.3/a', probe, 20)).toBe(false)
  })
})
