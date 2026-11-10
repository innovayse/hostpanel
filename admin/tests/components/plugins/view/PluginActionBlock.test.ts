/**
 * Unit tests for PluginActionBlock: the call with its filled path, the plugin's sentence, the refusal's
 * sentence, the confirmation, a label read from a route, the refresh of named blocks and the follow-up
 * that stops once its condition holds.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS } from '@/lib/pluginView'
import { PluginBlockKind, PluginBlockTone } from '@/types/pluginview'
import { json, problem } from '../../../helpers/fetch'
import { requestsOf } from '../../../helpers/routes'
import { ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

/** The publish route of a site. */
const publish = { method: 'post', path: '/sites/{siteId}/publish' }

describe('PluginActionBlock', () => {
  it('calls the route with the page values and shows the plugin sentence', async () => {
    const { wrapper, fetch } = await mountPluginView([{ kind: PluginBlockKind.Action, label: 'Publish', route: publish }], {
      values: { siteId: '7' },
      routes: { [`POST ${ext('sites/7/publish')}`]: () => json({ message: 'Publishing.' }) },
    })

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()

    expect(requestsOf(fetch)).toContain(`POST ${ext('sites/7/publish')}`)
    expect(wrapper.get('[data-testid="plugin-action-message"]').text()).toBe('Publishing.')
  })

  it('shows the API sentence of a refusal', async () => {
    const { wrapper } = await mountPluginView([{ kind: PluginBlockKind.Action, label: 'Publish', route: publish }], {
      values: { siteId: '7' },
      routes: { [`POST ${ext('sites/7/publish')}`]: () => problem('Nothing to publish.', 409) },
    })

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-action-message"]').text()).toBe('Nothing to publish.')
  })

  it('sends nothing for a route whose parameter the page lacks', async () => {
    const { wrapper, fetch } = await mountPluginView([{ kind: PluginBlockKind.Action, label: 'Publish', route: publish }])

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()

    expect(fetch).not.toHaveBeenCalled()
  })

  it('asks first when the view wants a confirmation', async () => {
    const { wrapper, fetch } = await mountPluginView([
      { kind: PluginBlockKind.Action, label: 'Delete', route: { method: 'delete', path: '/cache' }, confirm: 'Delete the cache?', style: PluginBlockTone.Danger },
    ], {
      routes: { [`DELETE ${ext('cache')}`]: () => new Response(null, { status: 204 }) },
    })

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()
    expect(fetch).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Delete the cache?')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()
    expect(requestsOf(fetch)).toEqual([`DELETE ${ext('cache')}`])
  })

  it('takes its label from a route and refreshes the named blocks after a success', async () => {
    let version = 4
    const { wrapper, fetch } = await mountPluginView([
      { kind: PluginBlockKind.Action, id: 'publish', label: 'Publish', labelSource: { route: { method: 'get', path: '/state' }, field: 'label' }, route: { method: 'post', path: '/publish' }, refresh: ['publish'] },
    ], {
      routes: {
        [`GET ${ext('state')}`]: () => json({ label: `Publish version ${version}` }),
        [`POST ${ext('publish')}`]: () => {
          version += 1
          return json({})
        },
      },
    })
    expect(wrapper.get('[data-testid="plugin-action-button"]').text()).toBe('Publish version 4')

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-action-button"]').text()).toBe('Publish version 5')
    expect(requestsOf(fetch).filter((line) => line.startsWith('GET'))).toHaveLength(2)
  })

  it('follows up until the condition holds, then stops reading', async () => {
    vi.useFakeTimers()
    let reads = 0
    const { wrapper, fetch } = await mountPluginView([
      {
        kind: PluginBlockKind.Action,
        label: 'Build',
        route: { method: 'post', path: '/build' },
        followUp: { refresh: [], until: { route: { method: 'get', path: '/build' }, field: 'state', equals: 'done' } },
      },
    ], {
      routes: {
        [`POST ${ext('build')}`]: () => json({ message: 'Building.' }),
        [`GET ${ext('build')}`]: () => {
          reads += 1
          return json({ state: reads >= 2 ? 'done' : 'running' })
        },
      },
    })

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()
    await vi.advanceTimersByTimeAsync(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await vi.advanceTimersByTimeAsync(PLUGIN_FOLLOW_UP_INTERVAL_MS)
    await vi.advanceTimersByTimeAsync(PLUGIN_FOLLOW_UP_INTERVAL_MS * 3)

    expect(requestsOf(fetch).filter((line) => line === `GET ${ext('build')}`)).toHaveLength(2)
    // The work has ended, so the sentence that said it was running is taken down.
    expect(wrapper.get('[data-testid="plugin-action-message"]').text()).toBe('')
  })
})
