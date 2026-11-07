/**
 * Tests for PluginView: blocks in document order, and the context the blocks share (values fill route parameters).
 *
 * @module tests/components/plugin/View.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '~/constants/pluginFollowUp'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import type { PluginActionBlock, PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  vi.useRealTimers()
  await api?.close()
  api = undefined
  // Cleared before the globals are unstubbed, which takes the setup's `navigateTo` stand-in away with them.
  vi.mocked(navigateTo).mockClear()
  vi.unstubAllGlobals()
})

describe('PluginView', () => {
  it('renders the blocks in document order', () => {
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Text, text: 'one' },
      { kind: PluginBlockKind.Text, text: 'two' },
    ])

    expect(wrapper.findAll('[data-testid="plugin-text"]').map(t => t.text())).toEqual(['one', 'two'])
  })

  it('fills route parameters from its values, percent-encoded', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/a%2F..`]: { body: { status: 'ok' } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'status' } }], { values: { siteId: 'a/..' } })
    await settle()

    expect(api.requests.at(-1)?.url).toBe(`${ROUTES_URL}/sites/a%2F..`)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('ok')
  })
})

describe('PluginView follow-ups', () => {
  /** The route a follow-up watches. */
  const stateRoute = { method: 'get', path: '/sites/{siteId}/state' } as const
  /** The route an action's own `visibleWhen` reads. */
  const shownRoute = { method: 'get', path: '/sites/{siteId}/shown' } as const

  /**
   * An action whose follow-up watches the state route and refreshes the badge.
   *
   * @param id - The action's block id.
   * @param extra - Further members, e.g. a `visibleWhen`.
   * @returns The block.
   */
  const publishAction = (id: string, extra: Partial<PluginActionBlock> = {}): PluginActionBlock => ({
    kind: PluginBlockKind.Action,
    id,
    label: id,
    route: TEST_ROUTES.publish,
    followUp: { refresh: ['live'], until: { route: stateRoute, field: 'state', equals: 'done' } },
    ...extra,
  })

  /** The badge the follow-ups refresh; every read of it is counted. */
  const liveBadge: PluginBlock = {
    kind: PluginBlockKind.Badge,
    id: 'live',
    source: { route: TEST_ROUTES.site, field: 'liveText' },
  }

  /**
   * Starts the stub API that counts the reads of the badge's route and of the watched route.
   *
   * @param state - What the watched route answers for each of its reads (the read number counted from 1); a promise
   * holds the answer back.
   * @returns The read counts, by route.
   */
  const startCountingApi = async (
    state: (read: number) => Promise<string> | string = () => 'running',
  ): Promise<{ live: number, state: number, shown: boolean }> => {
    const counts = { live: 0, state: 0, shown: true }
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => {
        counts.live += 1
        return { body: { liveText: 'Live' } }
      },
      [`GET ${ROUTES_URL}/sites/7/state`]: async () => {
        counts.state += 1
        return { body: { state: await state(counts.state) } }
      },
      [`GET ${ROUTES_URL}/sites/7/shown`]: () => ({ body: { shown: counts.shown } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: () => {
        counts.shown = false
        return { body: { message: 'Started.' } }
      },
      [`POST ${ROUTES_URL}/sites/7/recheck`]: () => {
        counts.shown = true
        return { body: { message: 'Checked.' } }
      },
    })
    return counts
  }

  it('ends the watch when the view is left: no further read, and a read still out refreshes nothing', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    let answer: (value: string) => void = () => undefined
    const counts = await startCountingApi(() => new Promise<string>((resolve) => {
      answer = resolve
    }))
    const wrapper = mountBlocks([liveBadge, publishAction('publish')])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(counts.state).toBe(1)
    const liveBefore = counts.live

    wrapper.unmount()
    answer('running')
    await settle()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
    await settle()

    expect(counts.state).toBe(1)
    expect(counts.live).toBe(liveBefore)
  })

  it('replaces the watch of a block that was clicked again after it hid and came back, so one watch runs', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const counts = await startCountingApi()
    const wrapper = mountBlocks([
      liveBadge,
      publishAction('publish', {
        refresh: ['publish'],
        visibleWhen: { route: shownRoute, field: 'shown', equals: true },
      }),
      // Brings the publish button back: it reads its condition again, as a new instance of the block.
      {
        kind: PluginBlockKind.Action,
        id: 'recheck',
        label: 'Recheck',
        route: { method: 'post', path: '/sites/{siteId}/recheck' },
        refresh: ['publish'],
      },
    ])
    await settle()
    const buttons = (): string[] => wrapper.findAll('[data-testid="plugin-action-button"]').map(button => button.text())
    expect(buttons()).toEqual(['publish', 'Recheck'])

    await wrapper.findAll('[data-testid="plugin-action-button"]')[0]!.trigger('click')
    await settle()
    // The click itself hid the button; its watch goes on without it.
    expect(buttons()).toEqual(['Recheck'])
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(buttons()).toEqual(['publish', 'Recheck'])

    // The button is a new instance of the same block: clicking it again starts the watch afresh and ends the old one.
    await wrapper.findAll('[data-testid="plugin-action-button"]')[0]!.trigger('click')
    await settle()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()

    expect(counts.state).toBe(1)
  })

  it('keeps the watches of two blocks with their own ids apart', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const counts = await startCountingApi()
    const wrapper = mountBlocks([publishAction('first'), publishAction('second')])
    await settle()
    for (const button of wrapper.findAll('[data-testid="plugin-action-button"]')) {
      await button.trigger('click')
      await settle()
    }
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()

    // One read for each watch: a click on the second block did not end the first block's watch.
    expect(counts.state).toBe(2)
  })
})
