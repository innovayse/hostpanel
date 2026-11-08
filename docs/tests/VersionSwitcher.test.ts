// @vitest-environment happy-dom
/**
 * Component tests for the version switcher: the lookup of the open page in the other versions is owned by
 * the component, so it must be aborted when the list closes, when the page changes and when the component
 * is unmounted, and an answer that arrives after the abort must not change a link.
 */
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { ProbeResponse } from '../.vitepress/theme/versionLinks'
import VersionSwitcher from '../.vitepress/theme/components/VersionSwitcher.vue'

/** The theme data, page and site the mocked `useData` returns; reactive, so a test can change the page. */
const data = vi.hoisted(() => ({ theme: null as unknown, page: null as unknown, site: null as unknown }))

vi.mock('vitepress', () => ({
  useData: () => data,
}))

/** Address `versions.json` is read from in the tests. */
const VERSIONS_URL = '/docs/versions.json'

/** The statuses the tests answer with. */
enum Status {
  /** A page that exists. */
  Ok = 200,
  /** A page that does not exist. */
  NotFound = 404,
}

/** A request for a page that is waiting for the test to answer it. */
interface PendingProbe {
  /** The address asked for. */
  url: string
  /** The signal the component handed to `fetch`. */
  signal: AbortSignal
  /** How the component asked to handle a redirect. */
  redirect: RequestRedirect | undefined
  /** Answers the request with a status, even after the signal aborted. */
  answer: (status: number) => void
}

/** Requests for pages made so far, oldest first. */
let pending: PendingProbe[] = []

/** The mounted component of the running test, unmounted after it. */
let wrapper: VueWrapper | null = null

/**
 * The fake `fetch`: `versions.json` is answered at once, every page request waits for the test.
 * @param url - The address asked for.
 * @param init - The options of the request.
 * @returns The response.
 */
const fakeFetch = async (url: string, init?: RequestInit): Promise<unknown> => {
  if (url === VERSIONS_URL) {
    return {
      ok: true,
      status: Status.Ok,
      json: async () => ({
        versions: [
          { label: 'v0.4', link: '/docs/', kind: 'latest' },
          { label: 'v0.3', link: '/docs/v0.3/', kind: 'release' },
        ],
      }),
    }
  }
  return new Promise<ProbeResponse>((resolve) => {
    pending.push({
      url,
      signal: init?.signal as AbortSignal,
      redirect: init?.redirect,
      answer: (status) => resolve({ ok: status >= 200 && status < 300, status }),
    })
  })
}

/**
 * Mounts the switcher for the latest docs and waits for the list to be read.
 * @returns The mounted component.
 */
const mountSwitcher = async (): Promise<VueWrapper> => {
  const mounted = mount(VersionSwitcher)
  await flushPromises()
  wrapper = mounted
  return mounted
}

/**
 * The link the list offers for the other version.
 * @param mounted - The mounted component.
 * @returns The `href` of the entry for `v0.3`.
 */
const olderLink = (mounted: VueWrapper): string | undefined => {
  return mounted.findAll('a').find((link) => link.text().startsWith('v0.3'))?.attributes('href')
}

beforeEach(() => {
  pending = []
  data.theme = ref({ docsVersion: { kind: 'latest', label: 'v0.4', versionsUrl: VERSIONS_URL } })
  data.page = ref({ relativePath: 'operators/install.md' })
  data.site = ref({ base: '/docs/' })
  vi.stubGlobal('fetch', fakeFetch)
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  vi.unstubAllGlobals()
})

describe('VersionSwitcher', () => {
  it('looks for the open page in the other version when the list opens, with redirects not followed', async () => {
    const mounted = await mountSwitcher()

    await mounted.find('button').trigger('click')

    expect(pending).toHaveLength(1)
    expect(pending[0].url).toBe('/docs/v0.3/operators/install')
    expect(pending[0].redirect).toBe('manual')
    expect(pending[0].signal.aborted).toBe(false)
    expect(olderLink(mounted)).toBe('/docs/v0.3/')
  })

  it('offers the same page once the server says it exists', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')

    pending[0].answer(Status.Ok)
    await flushPromises()

    expect(olderLink(mounted)).toBe('/docs/v0.3/operators/install')
  })

  it('aborts the lookup when the list closes', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')
    const [probe] = pending

    await mounted.find('button').trigger('click')

    expect(probe.signal.aborted).toBe(true)
  })

  it('aborts the lookup of the old page when the page changes and looks again for the new one', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')
    const [first] = pending

    ;(data.page as ReturnType<typeof ref>).value = { relativePath: 'staff/orders.md' }
    await flushPromises()

    expect(first.signal.aborted).toBe(true)
    expect(pending).toHaveLength(2)
    expect(pending[1].url).toBe('/docs/v0.3/staff/orders')
    expect(pending[1].signal.aborted).toBe(false)
  })

  it('aborts the lookup when the component is unmounted', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')
    const [probe] = pending

    mounted.unmount()
    wrapper = null

    expect(probe.signal.aborted).toBe(true)
  })

  it('does not let an aborted answer change a link', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')
    const [stale] = pending

    ;(data.page as ReturnType<typeof ref>).value = { relativePath: 'staff/orders.md' }
    await flushPromises()
    // The answer for the old page arrives after the page changed and the request was aborted.
    stale.answer(Status.Ok)
    await flushPromises()

    expect(olderLink(mounted)).toBe('/docs/v0.3/')

    pending[1].answer(Status.Ok)
    await flushPromises()

    expect(olderLink(mounted)).toBe('/docs/v0.3/staff/orders')
  })

  it('does not change the checked links when the list was closed before the answer', async () => {
    const mounted = await mountSwitcher()
    await mounted.find('button').trigger('click')
    const [probe] = pending

    await mounted.find('button').trigger('click')
    probe.answer(Status.Ok)
    await flushPromises()
    await mounted.find('button').trigger('click')

    // Opening again asks again; until that answer comes the link is the home page of the version.
    expect(pending).toHaveLength(2)
    expect(olderLink(mounted)).toBe('/docs/v0.3/')
  })
})
