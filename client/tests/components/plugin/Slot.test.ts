/**
 * Tests for PluginSlot against a real stub backend: with no plugins it renders nothing at all, a
 * failed index read renders nothing, listed widgets render as titled cards in the index order (each
 * reading its own view with the slot's context, a broken one isolated beside the others), and the
 * server render holds no slot and asks the backend nothing, so a slow plugin never delays a page.
 *
 * @module tests/components/plugin/Slot.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createPinia } from 'pinia'
import PluginSlot from '~/components/plugin/Slot.vue'
import { mountClient } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS } from '~/tests/helpers/mountPlugin'
import { slotIndex, slotPath, slotWidgetPath, throwingView, workingView } from '~/tests/helpers/pluginSlotFixture'
import { startTransport } from '~/tests/helpers/stubTransport'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { PluginSlotId } from '~/types/pluginslotid'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

afterEach(async () => {
  await stub.close()
})

/**
 * Mounts a slot.
 *
 * @param slotId - The slot.
 * @param context - The slot's context.
 * @returns The mounted slot.
 */
const mountSlot = (slotId = PluginSlotId.DashboardCards, context?: Record<string, string>) =>
  mountClient(PluginSlot, { props: { slotId, ...(context ? { context } : {}) }, components: PLUGIN_COMPONENTS })

describe('PluginSlot', () => {
  it('renders nothing with no plugins', async () => {
    stub = await startTransport(() => ({ body: slotIndex(PluginSlotId.DashboardCards, []) }))
    const slot = mountSlot()

    await waitUntil(() => expect(stub.requests).toHaveLength(1))
    expect(slot.html()).toBe('<!--v-if-->')
  })

  it('renders nothing when the index cannot be read', async () => {
    // A 404, not a 503: the transport retries a failed GET once on a 5xx, which would make the count below two.
    stub = await startTransport(() => ({ status: 404, body: { type: 'PLUGIN_SLOT_NOT_FOUND', title: 'x', status: 404, detail: 'x' } }))
    const slot = mountSlot()

    await waitUntil(() => expect(stub.requests).toHaveLength(1))
    expect(slot.html()).toBe('<!--v-if-->')
  })

  it('renders each listed widget as a titled card', async () => {
    stub = await startTransport(routeTable({
      [`GET ${slotPath(PluginSlotId.DashboardCards)}`]: { body: slotIndex(PluginSlotId.DashboardCards, ['ok']) },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'ok')}`]: { body: workingView() },
    }))
    const slot = mountSlot()

    await waitUntil(() => expect(slot.text()).toContain('Site is live'))
    expect(slot.find('[data-testid="plugin-slot-client.dashboard.cards"]').exists()).toBe(true)
    expect(slot.text()).toContain('Widget ok')
  })

  it('isolates a throwing widget beside a working one', async () => {
    stub = await startTransport(routeTable({
      [`GET ${slotPath(PluginSlotId.DashboardCards)}`]: { body: slotIndex(PluginSlotId.DashboardCards, ['broken', 'ok']) },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'broken')}`]: { body: throwingView() },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'ok')}`]: { body: workingView() },
    }))
    const slot = mountSlot()

    await waitUntil(() => expect(slot.find('[data-testid="plugin-slot-unavailable-acme-sites-broken"]').exists()).toBe(true))
    await waitUntil(() => expect(slot.get('[data-testid="plugin-slot-widget-acme-sites-ok"]').text()).toBe('Site is live'))
  })

  it('reads the index and each widget with the slot\'s context', async () => {
    stub = await startTransport(routeTable({
      [`GET ${slotPath(PluginSlotId.ServiceTabs)}`]: { body: slotIndex(PluginSlotId.ServiceTabs, ['ok']) },
      [`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`]: { body: workingView() },
    }))
    const slot = mountSlot(PluginSlotId.ServiceTabs, { serviceId: '4' })

    await waitUntil(() => expect(slot.text()).toContain('Site is live'))
    expect(requestsTo(stub.requests, slotPath(PluginSlotId.ServiceTabs)).map(queryOf)).toEqual([{ serviceId: '4' }])
    expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')).map(queryOf)).toEqual([{ serviceId: '4' }])
  })

  it('10. reads every widget again with a new context', async () => {
    stub = await startTransport(routeTable({
      [`GET ${slotPath(PluginSlotId.ServiceTabs)}`]: { body: slotIndex(PluginSlotId.ServiceTabs, ['ok', 'other']) },
      [`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`]: { body: workingView() },
      [`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'other')}`]: { body: workingView() },
    }))
    const slot = mountSlot(PluginSlotId.ServiceTabs, { serviceId: '4' })
    await waitUntil(() => expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'other'))).toHaveLength(1))

    await slot.setProps({ context: { serviceId: '5' } })

    for (const id of ['ok', 'other']) {
      await waitUntil(() => expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, id)).map(queryOf))
        .toEqual([{ serviceId: '4' }, { serviceId: '5' }]))
    }
  })

  it('renders nothing on the server and calls nothing there', async () => {
    stub = await startTransport(() => ({ body: slotIndex(PluginSlotId.DashboardCards, ['ok']) }))
    const app = createSSRApp({ render: () => h('main', [h('p', 'Host page'), h(PluginSlot, { slotId: PluginSlotId.DashboardCards })]) })
    app.use(createPinia())
    for (const [name, component] of Object.entries(PLUGIN_COMPONENTS)) {
      app.component(name, component)
    }

    const html = await renderToString(app)

    expect(html).toBe('<main><p>Host page</p><!--v-if--></main>')
    expect(stub.requests).toEqual([])
  })
})
