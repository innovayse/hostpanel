/**
 * Tests for PluginActionBlock: the call with the CSRF-carrying transport, the confirmation, the plugin's sentence,
 * a refusal's sentence, refreshing named blocks, opening the named page, and the follow-up refresh with its host
 * bounds and its end when the block leaves the page.
 *
 * @module tests/components/plugin/ActionBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS } from '~/constants/pluginFollowUp'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
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

/** The publish button of the worked example. */
const publish = { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish } as const

describe('PluginActionBlock', () => {
  it('calls the route and shows the plugin\'s message as text', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { message: '<b>Published</b>' } } })
    const wrapper = mountBlocks([publish])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(api.requests.at(-1)?.method).toBe('POST')
    expect(wrapper.get('[data-testid="plugin-action-message"]').text()).toBe('<b>Published</b>')
    expect(wrapper.find('[data-testid="plugin-action-message"] b').exists()).toBe(false)
  })

  it('asks first when the view sets a confirmation, and calls nothing on cancel', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 } })
    const wrapper = mountBlocks([{ ...publish, confirm: 'Publish now?' }])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    expect(wrapper.get('[data-testid="ui-confirm-dialog"]').text()).toContain('Publish now?')
    await wrapper.get('[data-testid="ui-confirm-cancel"]').trigger('click')
    await settle()
    expect(api.requests).toHaveLength(0)

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await wrapper.get('[data-testid="ui-confirm-confirm"]').trigger('click')
    await settle()
    expect(api.requests).toHaveLength(1)
  })

  it('shows the refusal\'s sentence', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: problem(409, 'The site is building.') })
    const wrapper = mountBlocks([publish])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(wrapper.get('[data-testid="plugin-action-message"]').text()).toBe('The site is building.')
  })

  it('reads the named blocks again after a success', async () => {
    let reads = 0
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { status: `v${++reads}` } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 },
    })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Badge, id: 'status', source: { route: TEST_ROUTES.site, field: 'status' } },
      { ...publish, refresh: ['status'] },
    ])
    await settle()
    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('v1')

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('v2')
  })

  it('opens the named page with parameters from the reply, and only a page of the index', async () => {
    api = await startPluginApi({ [`POST ${ROUTES_URL}/sites/7/publish`]: { body: { site: { id: 'a/b' } } } })
    const wrapper = mountBlocks([
      { ...publish, navigate: { page: 'site', params: { siteId: 'site.id' } } },
      { ...publish, label: 'Elsewhere', navigate: { page: 'not-in-index', params: { siteId: 'site.id' } } },
    ])

    const [known, unknown] = wrapper.findAll('[data-testid="plugin-action-button"]')
    await known!.trigger('click')
    await settle()
    expect(navigateTo).toHaveBeenCalledWith('/client/apps/acme-sites/sites/a%2Fb')

    vi.mocked(navigateTo).mockClear()
    await unknown!.trigger('click')
    await settle()
    expect(navigateTo).not.toHaveBeenCalled()
  })

  describe('follow-up', () => {
    /** The publish button, watching the site until it is live and refreshing the status badge meanwhile. */
    const watching = {
      ...publish,
      followUp: { refresh: ['status'], until: { route: TEST_ROUTES.site, field: 'status', equals: ['live', 'failed'] } },
    }

    /** The status badge the follow-up refreshes. */
    const badge = { kind: PluginBlockKind.Badge, id: 'status', source: { route: TEST_ROUTES.site, field: 'status' } } as const

    /**
     * The reads of the site route the stub received.
     *
     * @returns How many.
     */
    const siteReads = (): number => api!.requests.filter(request => request.method === 'GET').length

    it('shows the final value without a reload, then stops reading', async () => {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
      let reads = 0
      api = await startPluginApi({
        [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { status: ++reads < 4 ? 'publishing' : 'live' } }),
        [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 202 },
      })
      const wrapper = mountBlocks([badge, watching])
      await settle()
      await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
      await settle()
      expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('publishing')

      for (let i = 0; i < 3; i += 1) {
        vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
        await settle()
      }
      expect(wrapper.get('[data-testid="plugin-badge"]').text()).toBe('live')

      const done = siteReads()
      vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS * 5)
      await settle()
      expect(siteReads()).toBe(done)
    })

    it('stops after the host\'s attempts when the value never becomes final', async () => {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
      api = await startPluginApi({
        [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'publishing' } },
        [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 202 },
      })
      const wrapper = mountBlocks([watching])
      await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
      await settle()

      for (let i = 0; i < PLUGIN_FOLLOW_UP_MAX_ATTEMPTS + 3; i += 1) {
        vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
        await settle()
      }

      expect(siteReads()).toBe(PLUGIN_FOLLOW_UP_MAX_ATTEMPTS)
    })

    it('reads nothing more once the page is left, and nothing at all after a refusal', async () => {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
      let refuse = false
      api = await startPluginApi({
        [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'publishing' } },
        [`POST ${ROUTES_URL}/sites/7/publish`]: () => (refuse ? problem(409, 'Busy.') : { status: 202 }),
      })
      const left = mountBlocks([watching])
      await left.get('[data-testid="plugin-action-button"]').trigger('click')
      await settle()
      vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS)
      await settle()
      expect(siteReads()).toBe(1)

      left.unmount()
      vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS * 5)
      await settle()
      expect(siteReads()).toBe(1)

      refuse = true
      const refused = mountBlocks([watching])
      await refused.get('[data-testid="plugin-action-button"]').trigger('click')
      await settle()
      vi.advanceTimersByTime(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)
      await settle()
      expect(siteReads()).toBe(1)
    })
  })

  it('takes its label from a route once read, keeping the written one when the value is empty', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { label: 'Publish version 3', blank: '' } } })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish, labelSource: { route: TEST_ROUTES.site, field: 'label' } },
      { kind: PluginBlockKind.Action, label: 'Publish', route: TEST_ROUTES.publish, labelSource: { route: TEST_ROUTES.site, field: 'blank' } },
    ])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-action-button"]').map(button => button.text())).toEqual(['Publish version 3', 'Publish'])
  })
})
