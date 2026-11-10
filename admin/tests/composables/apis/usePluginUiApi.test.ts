/**
 * Tests for the plugin ui API composable: the index, the page by its address, a route call with its
 * method and body, and a stream, through the real `useApi` transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { usePluginUiApi } from '@/composables/apis/usePluginUiApi'
import { HttpMethod } from '@/types/http'
import type { ServerSentEvent } from '@/types/http'
import { callOf, json, problem, stubFetch } from '../../helpers/fetch'
import { eventStream } from '../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('usePluginUiApi', () => {
  it('fetchUi reads the admin index', async () => {
    const mock = stubFetch(json({ menus: [], pages: [] }))

    expect(await usePluginUiApi().fetchUi()).toEqual({ menus: [], pages: [] })
    expect(callOf(mock).url).toBe('/api/admin/plugins/ui')
  })

  it('fetchPageByPath sends the decoded address once encoded as the query', async () => {
    const mock = stubFetch(json({ pluginId: 'sites', id: 'home', path: '/', title: 'Home', view: { schema: 1, blocks: [] }, externalOrigins: [] }))

    await usePluginUiApi().fetchPageByPath('sites', '/sites/կայք')

    expect(callOf(mock).url).toBe(`/api/admin/plugins/ui/sites/pages/by-path?path=${encodeURIComponent('/sites/կայք')}`)
  })

  it('callRoute sends the method and the JSON body to the plugin route', async () => {
    const mock = stubFetch(json({ message: 'Done.' }))

    expect(await usePluginUiApi().callRoute('sites', HttpMethod.Post, 'sites/7/publish', { force: true })).toEqual({ message: 'Done.' })
    expect(callOf(mock)).toEqual(expect.objectContaining({ url: '/api/admin/plugins/sites/ext/sites/7/publish', method: 'POST', body: { force: true } }))
  })

  it('callRoute rejects with the API refusal', async () => {
    stubFetch(problem('No such route.', 404))

    await expect(usePluginUiApi().callRoute('sites', HttpMethod.Get, 'nope')).rejects.toBeTruthy()
  })

  it('openStream posts the body and hands each event over', async () => {
    const mock = stubFetch(eventStream([{ event: 'delta', data: { text: 'Hi' } }]))
    const events: ServerSentEvent[] = []

    await usePluginUiApi().openStream('sites', 'chat', { message: 'Hello' }, (event) => events.push(event), new AbortController().signal)

    expect(callOf(mock)).toEqual(expect.objectContaining({ url: '/api/admin/plugins/sites/ext/chat', method: 'POST', body: { message: 'Hello' } }))
    expect(events).toEqual([{ type: 'delta', data: { text: 'Hi' } }])
  })
})
