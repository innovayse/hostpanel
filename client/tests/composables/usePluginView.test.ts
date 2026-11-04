/**
 * Tests for `composables/usePluginView.ts`, through the real store, API composable and transport
 * against a stub server: a block outside a view is a host bug; a source is read on mount, on a
 * refresh of its block id and on a change of its query; a refusal shows the API's sentence; and a
 * source the page cannot read calls nothing and says nothing.
 *
 * @module tests/composables/usePluginView.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { enableAutoUnmount } from '@vue/test-utils'
import { startHeldServer } from '../helpers/heldServer'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { providePluginView, usePluginSource, usePluginView } from '~/composables/usePluginView'
import type { PluginSource } from '~/types/pluginblock'
import { settle } from '~/tests/helpers/settle'

/** The stub playing the BFF. */
let stub: StubServer

/** The refresh counts the test view hands out. */
const refreshes = ref<Record<string, number>>({})

/** The query the test block reads with. */
const query = ref<Record<string, string>>({})

/** The source the test block reads; tests replace it. */
const source = ref<PluginSource>({ route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' })

/** A block that reads one source and shows the value, the error and whether it is unavailable. */
const Reader = defineComponent({
  setup() {
    const { value, error, unavailable } = usePluginSource(() => source.value, () => 'status', () => query.value)
    return () => h('div', [
      h('span', { 'data-testid': 'value' }, String(value.value ?? '')),
      h('span', { 'data-testid': 'error' }, error.value),
      h('span', { 'data-testid': 'unavailable' }, String(unavailable.value)),
    ])
  },
})

/** A view holding the reading block, for the page `/sites/7` of the test plugin. */
const Host = defineComponent({
  setup() {
    providePluginView({
      pluginId: 'acme-sites',
      values: { siteId: '7' },
      externalOrigins: [],
      refreshes,
      refresh: () => undefined,
      open: () => undefined,
      track: () => undefined,
    })
    return () => h(Reader)
  },
})

/**
 * The route calls the stub received.
 *
 * @returns The URLs, oldest first.
 */
const calls = (): string[] => stub.requests.filter(request => request.url.includes('/routes/')).map(request => request.url)

beforeEach(async () => {
  refreshes.value = {}
  query.value = {}
  source.value = { route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' }
  let reads = 0
  stub = await startTransport(() => {
    reads += 1
    return { body: { status: ['a', 'b', 'c'][reads - 1] ?? 'z' } }
  })
})

// The test refs live at module level: a reader left mounted by an earlier test would keep reading from the next stub.
enableAutoUnmount(afterEach)

afterEach(async () => {
  await stub.close()
})

describe('usePluginView', () => {
  it('keeps the latest read when an older one answers after it', async () => {
    const server = await startHeldServer()
    try {
      const wrapper = mountWithKit(Host)
      await vi.waitFor(() => expect(server.held).toHaveLength(1))
      query.value = { page: '2' }
      await nextTick()
      await vi.waitFor(() => expect(server.held).toHaveLength(2))

      server.held[1]?.answer({ status: 'newer' })
      // Real sockets: the newer answer is awaited until it is shown, so the older one surely lands after it.
      await vi.waitFor(() => expect(wrapper.get('[data-testid="value"]').text()).toBe('newer'))
      server.held[0]?.answer({ status: 'older' })
      await settle()
      await new Promise(resolve => setTimeout(resolve, 20))

      expect(wrapper.get('[data-testid="value"]').text()).toBe('newer')
    } finally {
      await server.close()
    }
  })

  it('refuses a block rendered outside a view', () => {
    let refusal: unknown
    const Lost = defineComponent({
      setup() {
        try {
          usePluginView()
        } catch (error: unknown) {
          refusal = error
        }
        return () => h('span')
      },
    })

    mountWithKit(Lost)

    expect(String(refusal)).toContain('outside a plugin view')
  })

  it('reads on mount, on a refresh of its own id and when its query changes', async () => {
    const wrapper = mountWithKit(Host)
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('a')

    refreshes.value = { other: 1 }
    await nextTick()
    await settle()
    expect(calls()).toHaveLength(1)

    refreshes.value = { other: 1, status: 1 }
    await nextTick()
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('b')

    query.value = { page: '2' }
    await nextTick()
    await settle()
    expect(wrapper.get('[data-testid="value"]').text()).toBe('c')
    expect(calls()).toEqual([
      '/api/portal/client/plugins/acme-sites/routes/sites/7',
      '/api/portal/client/plugins/acme-sites/routes/sites/7',
      '/api/portal/client/plugins/acme-sites/routes/sites/7?page=2',
    ])
  })

  it('shows the API\'s sentence when the read is refused, and no value', async () => {
    stub.respondWith(() => ({ status: 403, body: { statusMessage: 'You may not see this site.', data: { code: 'CLIENT_PERMISSION_DENIED' } } }))

    const wrapper = mountWithKit(Host)
    await settle()

    expect(wrapper.get('[data-testid="value"]').text()).toBe('')
    expect(wrapper.get('[data-testid="error"]').text()).toBe('You may not see this site.')
    expect(wrapper.get('[data-testid="unavailable"]').text()).toBe('false')
  })

  it.each([
    [{ method: 'post', path: '/sites/{siteId}' }, 'a source that is not a GET'],
    [{ method: 'get', path: '/sites/{siteId}/posts/{postId}' }, 'a parameter the page does not carry'],
  ])('reads nothing for %j (%s) and has no sentence of its own', async (route, _reason: string) => {
    source.value = { route, field: 'status' }

    const wrapper = mountWithKit(Host)
    await settle()

    expect(calls()).toHaveLength(0)
    expect(wrapper.get('[data-testid="unavailable"]').text()).toBe('true')
    expect(wrapper.get('[data-testid="error"]').text()).toBe('')
  })
})
