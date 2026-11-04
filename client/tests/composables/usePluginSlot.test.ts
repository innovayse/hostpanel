/**
 * Tests for usePluginSlot against a real stub backend: nothing is read before mount (nor on the
 * server), the index is read once mounted with the slot's context, again when the context changes
 * (the old widgets cleared at once, an out-of-order reply to the old context dropped), and a refused
 * read leaves the widgets empty.
 *
 * @module tests/composables/usePluginSlot.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createSSRApp, defineComponent, h, ref } from 'vue'
import type { Ref } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createPinia } from 'pinia'
import { usePluginSlot } from '~/composables/usePluginSlot'
import { mountClient } from '~/tests/helpers/mountClient'
import { SERVICE_NOT_FOUND, slotIndex } from '~/tests/helpers/pluginSlotFixture'
import { heldReply, queryOf } from '~/tests/helpers/stubRoutes'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { StubServer } from '~/tests/helpers/stubServer'
import { PluginSlotId } from '~/types/pluginslotid'

/** The stub playing the BFF. */
let stub: StubServer

/** The widget ids the host last rendered. */
let seen: string[] = []

/**
 * A host listing the widget ids of the service tabs slot for a service.
 *
 * @param serviceId - The service the host page shows.
 * @returns The host component.
 */
const hostFor = (serviceId: Ref<string>) => defineComponent({
  setup: () => {
    const { widgets } = usePluginSlot(PluginSlotId.ServiceTabs, () => ({ serviceId: serviceId.value }))
    return () => {
      seen = widgets.value.map(widget => widget.id)
      return h('div')
    }
  },
})

beforeEach(async () => {
  seen = []
  stub = await startTransport(() => ({ body: slotIndex(PluginSlotId.ServiceTabs, ['tab']) }))
})

afterEach(async () => {
  await stub.close()
})

describe('usePluginSlot', () => {
  it('reads the slot with its context once mounted, and again when the context changes', async () => {
    const serviceId = ref('4')
    mountClient(hostFor(serviceId))

    await vi.waitFor(() => expect(seen).toEqual(['tab']))
    expect(stub.requests[0]?.url).toBe('/api/portal/client/plugins/slots/client.service.tabs?serviceId=4')

    serviceId.value = '5'
    await vi.waitFor(() => expect(stub.requests).toHaveLength(2))
    expect(stub.requests[1]?.url).toBe('/api/portal/client/plugins/slots/client.service.tabs?serviceId=5')
  })

  it('reads again when the slot id a getter answers changes', async () => {
    const slotId = ref<PluginSlotId>(PluginSlotId.ServiceTabs)
    mountClient(defineComponent({
      setup: () => {
        usePluginSlot(() => slotId.value, () => ({ serviceId: '4' }))
        return () => h('div')
      },
    }))
    await vi.waitFor(() => expect(stub.requests).toHaveLength(1))

    slotId.value = PluginSlotId.DashboardCards
    await vi.waitFor(() => expect(stub.requests).toHaveLength(2))

    expect(stub.requests[1]?.url).toBe('/api/portal/client/plugins/slots/client.dashboard.cards?serviceId=4')
  })

  it('8. keeps the latest context\'s widgets when an older reply arrives after it', async () => {
    const held = heldReply()
    stub.respondWith(request => queryOf(request).serviceId === '4' ? held.reply() : { body: slotIndex(PluginSlotId.ServiceTabs, ['five']) })
    const serviceId = ref('4')
    mountClient(hostFor(serviceId))
    await vi.waitFor(() => expect(held.waiting()).toBe(1))

    serviceId.value = '5'
    await vi.waitFor(() => expect(seen).toEqual(['five']))
    held.release({ body: slotIndex(PluginSlotId.ServiceTabs, ['four']) })
    await vi.waitFor(() => expect(stub.requests).toHaveLength(2))
    await new Promise(resolve => setTimeout(resolve, 50))

    expect(seen).toEqual(['five'])
  })

  it('9. empties the widgets at once when the context changes, before the new reply', async () => {
    const serviceId = ref('4')
    mountClient(hostFor(serviceId))
    await vi.waitFor(() => expect(seen).toEqual(['tab']))
    const held = heldReply()
    stub.respondWith(held.reply)

    serviceId.value = '5'
    await vi.waitFor(() => expect(held.waiting()).toBe(1))

    expect(seen).toEqual([])
    held.release({ body: slotIndex(PluginSlotId.ServiceTabs, ['tab']) })
  })

  it('leaves the widgets empty when the backend refuses the service', async () => {
    stub.respondWith(() => SERVICE_NOT_FOUND)
    mountClient(hostFor(ref('9')))

    await vi.waitFor(() => expect(stub.requests).toHaveLength(1))
    expect(seen).toEqual([])
  })

  it('reads nothing while the host renders on the server', async () => {
    const app = createSSRApp(hostFor(ref('4')))
    app.use(createPinia())

    expect(await renderToString(app)).toBe('<div></div>')
    expect(stub.requests).toEqual([])
  })
})
