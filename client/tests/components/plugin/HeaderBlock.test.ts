/**
 * Tests for PluginHeaderBlock: the title as text, its actions beside it, and an action's own `visibleWhen`.
 *
 * @module tests/components/plugin/HeaderBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '~/constants/pluginFollowUp'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { PluginHeaderBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  vi.useRealTimers()
  await api?.close()
  api = undefined
})

describe('PluginHeaderBlock', () => {
  it('shows the title as text and draws each action', () => {
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: '<i>Site</i>',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish },
        { kind: PluginBlockKind.Action, label: 'Publish again', route: TEST_ROUTES.publish },
      ],
    }])

    const header = wrapper.get('[data-testid="plugin-header"]')
    expect(header.text()).toContain('<i>Site</i>')
    expect(header.find('i').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(b => b.text())).toEqual(['Publish', 'Publish again'])
  })

  it('hides an action whose own condition does not hold', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'building' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish, visibleWhen: { route: TEST_ROUTES.site, field: 'status', equals: 'live' } },
        { kind: PluginBlockKind.Action, label: 'Refresh', route: TEST_ROUTES.publish },
      ],
    }])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(b => b.text())).toEqual(['Refresh'])
  })
})

describe('PluginHeaderBlock action messages', () => {
  it('shows an action\'s sentence once below the row, not under its button', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { message: 'Published.' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [
        { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish },
        { kind: PluginBlockKind.Action, label: 'Other', route: TEST_ROUTES.publish },
      ],
    }])

    await wrapper.findAll('[data-testid="plugin-action-button"]')[0]!.trigger('click')
    await settle()

    const line = wrapper.get('[data-testid="plugin-header-message"]')
    expect(line.text()).toBe('Published.')
    expect(wrapper.get('[data-testid="plugin-header"]').find('[data-testid="plugin-header-message"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-action-message"]').exists()).toBe(false)
  })

  it('shows a refusal\'s sentence below the row in the danger tone', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: problem(409, 'The site is building.') })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      title: 'Site',
      actions: [{ kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish }],
    }])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    const line = wrapper.get('[data-testid="plugin-header-message"]')
    expect(line.text()).toBe('The site is building.')
    expect(line.classes().join(' ')).toMatch(/danger|red/)
  })

  it('draws a compact line from routes, with any block kind beside it', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { name: 'Eva Bakery', domain: 'eva.test', live: 'Live: version 2' } } })
    const wrapper = mountBlocks([{
      kind: PluginBlockKind.Header,
      compact: true,
      titleSource: { route: TEST_ROUTES.site, field: 'name' },
      subtitleSource: { route: TEST_ROUTES.site, field: 'domain' },
      actions: [{ kind: PluginBlockKind.Badge, source: { route: TEST_ROUTES.site, field: 'live' } }],
    }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-header"]').text()).toBe('Eva Bakery')
    expect(wrapper.get('[data-testid="plugin-header-subtitle"]').text()).toBe('eva.test')
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 2')
  })
})

describe('PluginHeaderBlock publish follow-up', () => {
  /** The route the badge reads: the live version's text. */
  const liveRoute = TEST_ROUTES.site
  /** The route the publish button's own `visibleWhen` reads: whether a version waits to be published. */
  const publishableRoute = { method: 'get', path: '/sites/{siteId}/publishable' } as const
  /** The route the follow-up watches: the state of the publish. */
  const stateRoute = { method: 'get', path: '/sites/{siteId}/state' } as const

  /**
   * The compact header of the builder: a live badge and a publish button that shows only while there is a version to
   * publish.
   */
  const builderHeader: PluginHeaderBlock = {
    kind: PluginBlockKind.Header,
    id: 'top',
    compact: true,
    actions: [
      { kind: PluginBlockKind.Badge, id: 'liveState', source: { route: liveRoute, field: 'liveText' } },
      {
        kind: PluginBlockKind.Action,
        id: 'publish',
        label: 'Publish',
        route: TEST_ROUTES.publish,
        visibleWhen: { route: publishableRoute, field: 'publishable', equals: true },
        refresh: ['liveState', 'publish'],
        followUp: {
          refresh: ['liveState', 'publish'],
          until: { route: stateRoute, field: 'state', equals: ['done', 'failed'] },
        },
      },
    ],
  }

  /**
   * When each route starts to answer the ended publish, as the number of its read that is the first to say so. Reads
   * are counted per route, so the test does not depend on which of the three routes a refresh reads first: a route
   * that is never to change keeps `Infinity`.
   */
  interface PublishTimeline {
    /** The read of the state route that finds the publish `done`. */
    doneAtStateRead: number
    /** The read of the publishable route that finds nothing left to publish, which hides the button. */
    hiddenAtPublishableRead: number
    /** The read of the live route that shows the new version. */
    newVersionAtLiveRead: number
  }

  /**
   * Starts the stub API. Each route counts its own reads and answers by that count against the timeline, so a publish
   * that ends "between two reads of one refresh" is stated as: the button's route says it ended one read before the
   * badge's route does.
   *
   * @returns The timeline to set and the number of reads each route has answered so far.
   */
  const startBuilderApi = async (): Promise<{ timeline: PublishTimeline, reads: Record<string, number> }> => {
    const timeline: PublishTimeline = {
      doneAtStateRead: Number.POSITIVE_INFINITY,
      hiddenAtPublishableRead: Number.POSITIVE_INFINITY,
      newVersionAtLiveRead: Number.POSITIVE_INFINITY,
    }
    const reads: Record<string, number> = { live: 0, publishable: 0, state: 0 }
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => {
        reads.live! += 1
        const liveText = reads.live! >= timeline.newVersionAtLiveRead ? 'Live: version 27' : 'Live: version 26'
        return { body: { liveText } }
      },
      [`GET ${ROUTES_URL}/sites/7/publishable`]: () => {
        reads.publishable! += 1
        return { body: { publishable: reads.publishable! < timeline.hiddenAtPublishableRead } }
      },
      [`GET ${ROUTES_URL}/sites/7/state`]: () => {
        reads.state! += 1
        return { body: { state: reads.state! >= timeline.doneAtStateRead ? 'done' : 'running' } }
      },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { message: 'Version 27 is being published to your live site.' } },
    })
    return { timeline, reads }
  }

  it('still shows the new live version when the publish ends between two reads of one refresh', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { timeline, reads } = await startBuilderApi()
    // Reads of each route by round: 1 on mount; 2 after the click (its own refresh); 3 after the first attempt; 4 after
    // the second. The button's route turns to "nothing left to publish" on its 3rd read, one round before the badge's
    // route shows the new version (4th), and the watch's own route says "done" on its 2nd read.
    timeline.hiddenAtPublishableRead = 3
    timeline.newVersionAtLiveRead = 4
    timeline.doneAtStateRead = 2
    const wrapper = mountBlocks([builderHeader])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 26')

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(reads).toEqual({ live: 3, publishable: 3, state: 1 })
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 26')

    // The hidden button must not end the watch: the next attempt reads the ended publish and refreshes the badge.
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(reads.state).toBe(2)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 27')
  })

  it('takes the "being published" sentence away once the publish has ended, and keeps it until then', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { timeline } = await startBuilderApi()
    timeline.doneAtStateRead = 2
    const wrapper = mountBlocks([builderHeader])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-header-message"]').text())
      .toBe('Version 27 is being published to your live site.')

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.find('[data-testid="plugin-header-message"]').exists()).toBe(true)

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.find('[data-testid="plugin-header-message"]').exists()).toBe(false)
  })

  it('leaves a newer sentence alone when the earlier publish ends', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { timeline } = await startBuilderApi()
    timeline.doneAtStateRead = 2
    const wrapper = mountBlocks([{
      ...builderHeader,
      actions: [
        ...(builderHeader.actions ?? []),
        {
          kind: PluginBlockKind.Action,
          id: 'other',
          label: 'Other',
          route: { method: 'post', path: '/sites/{siteId}/other' },
        },
      ],
    }])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    // Another action of the header answers while the publish still runs: the line now speaks for that one.
    await wrapper.findAll('[data-testid="plugin-action-button"]')[1]!.trigger('click')
    await settle()
    const newer = wrapper.get('[data-testid="plugin-header-message"]').text()
    expect(newer).not.toBe('Version 27 is being published to your live site.')

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.get('[data-testid="plugin-header-message"]').text()).toBe(newer)
  })
})
