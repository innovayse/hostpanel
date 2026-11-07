/**
 * Tests for PluginView: blocks in document order, and the context the blocks share (values fill route parameters).
 *
 * @module tests/components/plugin/View.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '~/constants/pluginFollowUp'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, ROUTES_URL, startPluginApi, stubStream, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
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

  it('renders every block of a view that gives one id to two blocks, with no duplicate key', async () => {
    // A plugin installed before the host refused repeated ids is still loaded with such a view (the host only warns).
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Text, id: 'a', text: 'one' },
      { kind: PluginBlockKind.Text, id: 'a', text: 'two' },
    ])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-text"]').map(t => t.text())).toEqual(['one', 'two'])
    expect(warn.mock.calls.flat().join(' ')).not.toContain('Duplicate keys')
    warn.mockRestore()
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

describe('PluginView shared reads', () => {
  /** The state route every block and card below names. */
  const state = TEST_ROUTES.site

  /** The ids the publish button and the chat refresh (not the chat's own, which would start a new conversation). */
  const WATCHED = ['top', 'live', 'credits', 'publish', 'failed', 'domain']

  /** What the stub's state route answers; the publish call starts the work and `finish` ends it. */
  const server = { live: 'Live: version 5', publishable: true, state: 'idle' }

  /**
   * Ends the work the publish call started.
   *
   * @returns Nothing.
   */
  const finish = (): void => {
    server.live = 'Live: version 6'
    server.state = 'done'
  }

  /**
   * A page the way a site builder lays it out: a header with a title and a line read from the state route, two badges
   * and a publish button in it, an alert, a stat, and a conversation whose result cards offer an action by the same state.
   *
   * @returns The blocks.
   */
  const builder = (): PluginBlock[] => [
    {
      kind: PluginBlockKind.Header,
      id: 'top',
      compact: true,
      titleSource: { route: state, field: 'name' },
      subtitleSource: { route: state, field: 'domain' },
      actions: [
        { kind: PluginBlockKind.Badge, id: 'live', source: { route: state, field: 'liveText' } },
        { kind: PluginBlockKind.Badge, id: 'credits', source: { route: state, field: 'creditsText' } },
        {
          kind: PluginBlockKind.Action,
          id: 'publish',
          label: 'Publish',
          labelSource: { route: state, field: 'publishLabel' },
          bodySource: { route: state, fields: { expectedVersion: 'publishVersion' } },
          visibleWhen: { route: state, field: 'canPublish', equals: true },
          route: TEST_ROUTES.publish,
          refresh: WATCHED,
          followUp: { refresh: WATCHED, until: { route: state, field: 'publishState', equals: 'done' } },
        },
      ],
    },
    { kind: PluginBlockKind.Alert, id: 'failed', text: 'Publishing failed.', visibleWhen: { route: state, field: 'publishState', equals: 'failed' } },
    { kind: PluginBlockKind.Stat, id: 'domain', label: 'Domain', source: { route: state, field: 'domain' } },
    { kind: PluginBlockKind.Chat, id: 'conversation', send: TEST_ROUTES.chat, refresh: WATCHED },
  ]

  /**
   * Starts the stub API and counts the reads of the state route.
   *
   * @returns The requests the state route received, to be read at any time.
   */
  const startBuilderApi = async (): Promise<() => number> => {
    server.live = 'Live: version 5'
    server.publishable = true
    server.state = 'idle'
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({
        body: {
          name: 'Bakery',
          domain: 'bakery.example.test',
          liveText: server.live,
          creditsText: '99 credits',
          publishLabel: 'Publish version 6',
          publishVersion: 6,
          canPublish: server.publishable,
          publishState: server.state,
        },
      }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: () => {
        // The work starts; it ends when the test says so, as the plugin's does some time after the call.
        server.publishable = false
        server.state = 'running'
        return { body: { message: 'Publishing.' } }
      },
    })
    return () => api?.requests.filter(request => request.method === 'GET' && request.url === `${ROUTES_URL}/sites/7`).length ?? 0
  }

  /**
   * The text of the badges of the page, in order.
   *
   * @param wrapper - The mounted view.
   * @returns The texts.
   */
  const badges = (wrapper: ReturnType<typeof mountBlocks>): string[] => wrapper.findAll('[data-testid="plugin-badge"]').map(badge => badge.text())

  it('reads the state route once for every block and card of a page that names it, on load and on each refresh', async () => {
    const stateReads = await startBuilderApi()
    const wrapper = mountBlocks(builder())
    await settle()

    // Header title and line, two badges, the button's label, body and condition, the alert's condition and the stat.
    expect(stateReads()).toBe(1)
    expect(wrapper.get('[data-testid="plugin-header"]').text()).toBe('Bakery')
    expect(badges(wrapper)).toEqual(['Live: version 5', '99 credits'])

    // A conversation turn ends: the result card offers an action by the same state, and the blocks read again.
    const reply = stubStream()
    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Go')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()
    reply.send('result', {
      title: 'Version 6 is ready',
      actions: [
        { label: 'Publish version 6', route: TEST_ROUTES.publish, visibleWhen: { route: state, field: 'publishVersion', equalsAny: [6] } },
        { label: 'Not now', route: TEST_ROUTES.publish, visibleWhen: { route: state, field: 'canPublish', equalsAny: [true] } },
      ],
    })
    await settle()
    // The cards were drawn while the stream is open, after the turn began: both took one new read, not the picture from before it.
    expect(stateReads()).toBe(2)
    reply.close()
    await settle()

    // The turn ended: the page, the cards and the conversation's own blocks read again, once.
    expect(stateReads()).toBe(3)

    await wrapper.get('[data-testid="plugin-header-block"] [data-testid="plugin-action-button"]').trigger('click')
    await settle()

    // The publish call refreshed the page: one read for all of it, and the button went with the condition.
    expect(stateReads()).toBe(4)
    expect(wrapper.find('[data-testid="plugin-header-block"] [data-testid="plugin-action-button"]').exists()).toBe(false)
  })

  it('shows a result card the action the turn made applicable, though the chat names no block to read again', async () => {
    let publishVersion = 5
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { publishVersion } }) })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Stat, id: 'version', label: 'Version', source: { route: state, field: 'publishVersion' } },
      { kind: PluginBlockKind.Chat, id: 'conversation', send: TEST_ROUTES.chat },
    ])
    await settle()
    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('5')

    const reply = stubStream()
    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Go')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()
    // The turn changes what the card's condition checks, and the card is drawn while the stream is still open.
    publishVersion = 6
    reply.send('result', {
      title: 'Version 6 is ready',
      actions: [{ label: 'Publish version 6', route: TEST_ROUTES.publish, visibleWhen: { route: state, field: 'publishVersion', equalsAny: [6] } }],
    })
    await settle()

    const card = wrapper.get('[data-testid="plugin-chat-result-actions"]')
    expect(card.attributes('style') ?? '').not.toContain('display: none')
    expect(card.get('[data-testid="plugin-action-button"]').text()).toBe('Publish version 6')
  })

  it('reads the state route once per poll of a follow-up, and shows the poll\'s answer in every block at once', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const stateReads = await startBuilderApi()
    const wrapper = mountBlocks(builder())
    await settle()
    expect(stateReads()).toBe(1)

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(stateReads()).toBe(2)
    expect(badges(wrapper)).toEqual(['Live: version 5', '99 credits'])

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    // The poll's read and the refresh it asks for are one request, not one for each block.
    expect(stateReads()).toBe(3)

    finish()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()

    expect(stateReads()).toBe(4)
    expect(badges(wrapper)).toEqual(['Live: version 6', '99 credits'])
    expect(wrapper.get('[data-testid="plugin-header-subtitle"]').text()).toBe('bakery.example.test')
  })
})
