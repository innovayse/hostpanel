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
  /** What the site route answers while the version waits on the preview. */
  const ready = { canPublish: true, publishState: 'none', liveText: 'Live: version 26' }
  /** While the publish runs: the live site still shows the old version. */
  const running = { canPublish: true, publishState: 'running', liveText: 'Live: version 26' }
  /** Once it ended: the version is live, and there is nothing left to publish. */
  const done = { canPublish: false, publishState: 'done', liveText: 'Live: version 27' }

  /** The compact header of the builder: a live badge and a publish button that shows only while there is a version to publish. */
  const builderHeader: PluginHeaderBlock = {
    kind: PluginBlockKind.Header,
    id: 'top',
    compact: true,
    actions: [
      { kind: PluginBlockKind.Badge, id: 'liveState', source: { route: TEST_ROUTES.site, field: 'liveText' } },
      {
        kind: PluginBlockKind.Action,
        id: 'publish',
        label: 'Publish',
        route: TEST_ROUTES.publish,
        visibleWhen: { route: TEST_ROUTES.site, field: 'canPublish', equals: true },
        refresh: ['liveState', 'publish'],
        followUp: {
          refresh: ['liveState', 'publish'],
          until: { route: TEST_ROUTES.site, field: 'publishState', equals: ['done', 'failed'] },
        },
      },
    ],
  }

  /**
   * Starts the stub API with the site route answering the current phase, and the publish route answering its sentence.
   * The phase turns to `done` once `readsBeforeDone` reads of the site route have been answered, which is how a
   * publish can end between two reads of one refresh.
   *
   * @returns The phase control: set `readsBeforeDone` to the number of reads still to answer as `running`.
   */
  const startBuilderApi = async (): Promise<{ readsBeforeDone: number }> => {
    const control = { readsBeforeDone: Number.POSITIVE_INFINITY }
    let phase: Record<string, unknown> = ready
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => {
        control.readsBeforeDone -= 1
        if (control.readsBeforeDone < 0) {
          phase = done
        }
        return { body: phase }
      },
      [`POST ${ROUTES_URL}/sites/7/publish`]: () => {
        phase = running
        return { body: { message: 'Version 27 is being published to your live site.' } }
      },
    })
    return control
  }

  it('still shows the new live version when the publish ends between two reads of one refresh', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const control = await startBuilderApi()
    const wrapper = mountBlocks([builderHeader])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 26')

    // The follow-up's own read and the badge's read answer "running"; the publish ends before the button's condition is
    // read, so the button hides in the same round in which the badge was last read as the old version.
    control.readsBeforeDone = 2
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 26')

    // The hidden button must not end the watch: the next attempt reads the ended publish and refreshes the badge.
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 27')
  })

  it('takes the "being published" sentence away once the publish has ended, and keeps it until then', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const control = await startBuilderApi()
    const wrapper = mountBlocks([builderHeader])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-header-message"]').text()).toBe('Version 27 is being published to your live site.')

    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.find('[data-testid="plugin-header-message"]').exists()).toBe(true)

    control.readsBeforeDone = 0
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('Live: version 27')
    expect(wrapper.find('[data-testid="plugin-header-message"]').exists()).toBe(false)
  })

  it('leaves a newer sentence alone when the earlier publish ends', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const control = await startBuilderApi()
    const wrapper = mountBlocks([{
      ...builderHeader,
      actions: [...(builderHeader.actions ?? []), { kind: PluginBlockKind.Action, id: 'other', label: 'Other', route: { method: 'post', path: '/sites/{siteId}/other' } }],
    }])
    await settle()
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    // Another action of the header answers while the publish still runs: the line now speaks for that one.
    await wrapper.findAll('[data-testid="plugin-action-button"]')[1]!.trigger('click')
    await settle()
    const newer = wrapper.get('[data-testid="plugin-header-message"]').text()
    expect(newer).not.toBe('Version 27 is being published to your live site.')

    control.readsBeforeDone = 0
    vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await settle()
    expect(wrapper.get('[data-testid="plugin-header-message"]').text()).toBe(newer)
  })
})
