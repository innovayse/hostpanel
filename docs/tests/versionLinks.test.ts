/**
 * Unit tests for the links of the version switcher: the root of the docs, which links of `versions.json`
 * are accepted, the fall back to the home page of a version when the page is missing there, and the
 * bounds of the lookup (abort, cache, cap and concurrency).
 */
import { describe, expect, it } from 'vitest'
import {
  MAX_CACHED_ANSWERS,
  MAX_CONCURRENT_PROBES,
  MAX_PROBED_ENTRIES,
  ProbeMethod,
  ProbeStatus,
  VersionKind,
  docsRootOf,
  findPages,
  hrefFor,
  pageExists,
  pageIn,
  parseVersions,
  safeVersionLink,
} from '../.vitepress/theme/versionLinks'
import type { Probe, ProbeResponse, VersionEntry } from '../.vitepress/theme/versionLinks'

/** The entry of an older release below `/docs/`. */
const release: VersionEntry = { label: 'v0.3', link: '/docs/v0.3/', kind: VersionKind.Release }

/** The statuses the tests answer with. */
enum Status {
  /** A page that exists. */
  Ok = 200,
  /** A redirect that was not followed. */
  Found = 302,
  /** A refused request. */
  BadRequest = 400,
  /** A forbidden request. */
  Forbidden = 403,
  /** A page that does not exist. */
  NotFound = 404,
  /** A server failure. */
  ServerError = 500,
}

describe('docsRootOf', () => {
  it('is the base itself for the latest docs', () => {
    expect(docsRootOf('/docs/', VersionKind.Latest, 'v0.4')).toBe('/docs/')
    expect(docsRootOf('/docs/', VersionKind.Latest, '')).toBe('/docs/')
  })

  it('cuts the version off the base of an older release and of next', () => {
    expect(docsRootOf('/docs/v0.3/', VersionKind.Release, 'v0.3')).toBe('/docs/')
    expect(docsRootOf('/docs/next/', VersionKind.Next, 'next')).toBe('/docs/')
  })

  it('handles a docs site served at the root of a domain', () => {
    expect(docsRootOf('/', VersionKind.Latest, '')).toBe('/')
    expect(docsRootOf('/v0.3/', VersionKind.Release, 'v0.3')).toBe('/')
  })

  it('normalizes a base without slashes and leaves a base that does not end with the label', () => {
    expect(docsRootOf('docs/v0.3', VersionKind.Release, 'v0.3')).toBe('/docs/')
    expect(docsRootOf('/docs/', VersionKind.Release, 'v0.3')).toBe('/docs/')
  })

  it('does not cut a segment that only ends like the label', () => {
    expect(docsRootOf('/docs/xv0.3/', VersionKind.Release, 'v0.3')).toBe('/docs/xv0.3/')
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

  it.each([
    ['an encoded slash climb', '/docs/..%2f..%2fadmin/'],
    ['an upper case encoded slash', '/docs/..%2F..%2Fadmin/'],
    ['an encoded slash inside a name', '/docs/v0.3%2fx/'],
    ['an encoded backslash climb', '/docs/..%5c..%5cadmin/'],
    ['an upper case encoded backslash', '/docs/..%5C..%5Cadmin/'],
    ['an encoded null', '/docs/v0.3%00/'],
    ['a raw null', '/docs/v0.3/\u0000'],
    ['a null in the middle', '/docs/v0.3\u0000/x/'],
  ])('refuses %s', (_name, link) => {
    expect(safeVersionLink(link, '/docs/')).toBeNull()
  })

  it.each([[undefined], [null], [42], [{}], [['/docs/']]])('refuses a value that is not a string (%j)', (value) => {
    expect(safeVersionLink(value, '/docs/')).toBeNull()
  })

  it('keeps an escape that is not a separator', () => {
    expect(safeVersionLink('/docs/v0.3%2d/', '/docs/')).toBe('/docs/v0.3%2d/')
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

  it('keeps the first entry of a label and drops the later ones', () => {
    const body = {
      versions: [
        { label: 'v0.3', link: '/docs/v0.3/', kind: 'release' },
        { label: 'v0.3', link: '/docs/other/', kind: 'release' },
        { label: 'next', link: '/docs/next/', kind: 'next' },
      ],
    }

    expect(parseVersions(body, '/docs/')).toEqual([
      { label: 'v0.3', link: '/docs/v0.3/', kind: VersionKind.Release },
      { label: 'next', link: '/docs/next/', kind: VersionKind.Next },
    ])
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

/**
 * A response with a status.
 * @param status The status code to answer with.
 * @returns The response, successful for a 2xx status.
 */
const answer = (status: number): ProbeResponse => ({ ok: status >= 200 && status < 300, status })

/**
 * A request that never answers and rejects when it is aborted, like `fetch`.
 * @param signals Receives the signal of every request made.
 * @returns The probe.
 */
const hangingProbe = (signals: AbortSignal[] = []): Probe => {
  return (_url, init) =>
    new Promise((_resolve, reject) => {
      signals.push(init.signal)
      init.signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))
    })
}

describe('pageExists', () => {
  it('is true for a 2xx answer to HEAD', async () => {
    const methods: string[] = []
    const probe: Probe = async (_url, init) => {
      methods.push(init.method)
      return answer(Status.Ok)
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(true)
    expect(methods).toEqual([ProbeMethod.Head])
  })

  it('is false for a 404', async () => {
    expect(await pageExists('/docs/v0.3/a', async () => answer(Status.NotFound))).toBe(false)
  })

  it.each([[Status.BadRequest], [Status.Forbidden]])('is false, without asking again, for a %i answer to HEAD', async (status) => {
    const methods: string[] = []
    const probe: Probe = async (_url, init) => {
      methods.push(init.method)
      return answer(status)
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(false)
    expect(methods).toEqual([ProbeMethod.Head])
  })

  it('is false for a server error and for a redirect that was not followed', async () => {
    expect(await pageExists('/docs/v0.3/a', async () => answer(Status.ServerError))).toBe(false)
    expect(await pageExists('/docs/v0.3/a', async () => answer(Status.Found))).toBe(false)
  })

  it.each([[ProbeStatus.MethodNotAllowed], [ProbeStatus.NotImplemented]])('asks again with GET when HEAD answers %i', async (status) => {
    const methods: string[] = []
    const probe: Probe = async (_url, init) => {
      methods.push(init.method)
      return init.method === ProbeMethod.Head ? answer(status) : answer(Status.Ok)
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(true)
    expect(methods).toEqual([ProbeMethod.Head, ProbeMethod.Get])
  })

  it('cancels the body of the GET answer', async () => {
    let cancelled = 0
    const probe: Probe = async (_url, init) => {
      if (init.method === ProbeMethod.Head) {
        return answer(ProbeStatus.MethodNotAllowed)
      }
      return {
        ...answer(Status.Ok),
        body: {
          cancel: async () => {
            cancelled += 1
          },
        },
      }
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(true)
    expect(cancelled).toBe(1)
  })

  it('is false when the request fails', async () => {
    const probe: Probe = async () => {
      throw new TypeError('network error')
    }

    expect(await pageExists('/docs/v0.3/a', probe)).toBe(false)
  })

  it('is false when no answer comes in time', async () => {
    expect(await pageExists('/docs/v0.3/a', hangingProbe(), 20)).toBe(false)
  })

  it('is false, without a request, for a signal that is already aborted', async () => {
    const controller = new AbortController()
    controller.abort()
    let calls = 0
    const probe: Probe = async () => {
      calls += 1
      return answer(Status.Ok)
    }

    expect(await pageExists('/docs/v0.3/a', probe, 20, controller.signal)).toBe(false)
    expect(calls).toBe(0)
  })

  it('aborts the request in flight when the signal aborts', async () => {
    const controller = new AbortController()
    const signals: AbortSignal[] = []

    const result = pageExists('/docs/v0.3/a', hangingProbe(signals), 60000, controller.signal)
    controller.abort()

    expect(await result).toBe(false)
    expect(signals).toHaveLength(1)
    expect(signals[0].aborted).toBe(true)
  })
})

/**
 * An older release entry below `/docs/`.
 * @param label The label of the entry.
 * @returns The entry.
 */
const entryOf = (label: string): VersionEntry => ({ label, link: `/docs/${label}/`, kind: VersionKind.Release })

/**
 * A list of older release entries named `v0.0`, `v0.1` and so on.
 * @param count How many entries.
 * @returns The entries.
 */
const entriesOf = (count: number): VersionEntry[] => Array.from({ length: count }, (_value, index) => entryOf(`v0.${index}`))

describe('findPages', () => {
  it('gives the same page for a version that has it and the home page for one that has not', async () => {
    const probe: Probe = async (url) => answer(url.startsWith('/docs/v0.1/') ? Status.Ok : Status.NotFound)

    const links = await findPages([entryOf('v0.1'), entryOf('v0.2')], 'a/b', probe, { signal: new AbortController().signal, cache: new Map() })

    expect(links).toEqual({ 'v0.1': '/docs/v0.1/a/b', 'v0.2': '/docs/v0.2/' })
  })

  it('asks for at most the capped number of entries and leaves the rest without a link', async () => {
    const asked: string[] = []
    const probe: Probe = async (url) => {
      asked.push(url)
      return answer(Status.Ok)
    }

    const links = await findPages(entriesOf(MAX_PROBED_ENTRIES + 5), 'a', probe, { signal: new AbortController().signal, cache: new Map() })

    expect(asked).toHaveLength(MAX_PROBED_ENTRIES)
    expect(Object.keys(links)).toHaveLength(MAX_PROBED_ENTRIES)
    expect(links[`v0.${MAX_PROBED_ENTRIES}`]).toBeUndefined()
  })

  it('never has more requests in flight than the limit', async () => {
    let running = 0
    let peak = 0
    const probe: Probe = async () => {
      running += 1
      peak = Math.max(peak, running)
      await new Promise((resolve) => setTimeout(resolve, 5))
      running -= 1
      return answer(Status.Ok)
    }

    await findPages(entriesOf(MAX_PROBED_ENTRIES), 'a', probe, { signal: new AbortController().signal, cache: new Map() })

    expect(peak).toBe(MAX_CONCURRENT_PROBES)
  })

  it('does not ask again for a page whose answer is in the cache, but asks for another page', async () => {
    let calls = 0
    const probe: Probe = async () => {
      calls += 1
      return answer(Status.Ok)
    }
    const options = { signal: new AbortController().signal, cache: new Map<string, boolean>() }

    await findPages(entriesOf(3), 'a', probe, options)
    const links = await findPages(entriesOf(3), 'a', probe, options)

    expect(calls).toBe(3)
    expect(links['v0.1']).toBe('/docs/v0.1/a')
    await findPages(entriesOf(3), 'b', probe, options)
    expect(calls).toBe(6)
  })

  it('keeps a missing answer in the cache as well', async () => {
    let calls = 0
    const probe: Probe = async () => {
      calls += 1
      return answer(Status.NotFound)
    }
    const options = { signal: new AbortController().signal, cache: new Map<string, boolean>() }

    await findPages([entryOf('v0.1')], 'a', probe, options)
    await findPages([entryOf('v0.1')], 'a', probe, options)

    expect(calls).toBe(1)
  })

  it('empties the cache before it grows past its limit', async () => {
    const cache = new Map<string, boolean>()
    for (let index = 0; index < MAX_CACHED_ANSWERS; index += 1) {
      cache.set(`/old/${index}`, true)
    }

    await findPages([entryOf('v0.1')], 'a', async () => answer(Status.Ok), { signal: new AbortController().signal, cache })

    expect([...cache.keys()]).toEqual(['/docs/v0.1/a'])
  })

  it('stops, returns nothing and caches nothing when the signal aborts', async () => {
    const controller = new AbortController()
    const cache = new Map<string, boolean>()
    const signals: AbortSignal[] = []

    const result = findPages(entriesOf(10), 'a', hangingProbe(signals), { signal: controller.signal, cache })
    controller.abort()

    expect(await result).toEqual({})
    expect(cache.size).toBe(0)
    expect(signals).toHaveLength(MAX_CONCURRENT_PROBES)
    expect(signals.every((signal) => signal.aborted)).toBe(true)
  })
})
