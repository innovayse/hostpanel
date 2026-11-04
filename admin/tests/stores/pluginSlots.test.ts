/**
 * Tests for the plugin slots store: loading per slot and record, the guard against a late answer,
 * and hiding a widget through the one hide setting row. Run through the real `useApi` transport.
 */

import { describe, expect, it, vi } from 'vitest'
import { usePluginSlotsStore } from '@/stores/pluginSlots'
import { PluginSlotId } from '@/types/pluginslot'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'
import { json, problem, stubOffline } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'
import { installStoreHooks } from '../helpers/storeHooks'

installStoreHooks()

/**
 * Builds a widget as the host lists it.
 *
 * @param id - Id of the widget; also its title.
 * @returns The widget.
 */
const widget = (id: string): PluginSlotWidgetDto => ({
  pluginId: 'sites', id, view: `views/${id}.json`, title: id, order: 1, contextKeys: ['clientId'],
})

/**
 * Builds the host's answer for the client panels slot.
 *
 * @param widgets - The widgets of the slot.
 * @returns The slot index.
 */
const clientPanels = (...widgets: PluginSlotWidgetDto[]) => ({ slotId: PluginSlotId.AdminClientPanels, widgets })

/** The one hide setting row; it already hides a widget of this slot and one of the service slot. */
const hiddenRow = {
  id: 5,
  key: 'plugins.hidden_slot_widgets',
  value: 'admin.client.panels:sites:old,admin.service.panels:sites:usage',
  isSecret: false,
  hasValue: true,
}

describe('pluginSlotsStore.load', () => {
  it('keeps the widgets under the slot and record', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots/admin.client.panels?clientId=1': () => json(clientPanels(widget('usage'))) })
    const store = usePluginSlotsStore()

    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([widget('usage')])
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 2 })).toEqual([])
    expect(store.widgetsFor(PluginSlotId.AdminServicePanels, { serviceId: 1 })).toEqual([])
  })

  it('never lets one record see the widgets of another', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots/admin.client.panels?clientId=1': () => json(clientPanels(widget('one'))),
      'GET /api/admin/plugins/slots/admin.client.panels?clientId=2': () => json(clientPanels(widget('two'))),
    })
    const store = usePluginSlotsStore()

    await Promise.all([
      store.load(PluginSlotId.AdminClientPanels, { clientId: 1 }),
      store.load(PluginSlotId.AdminClientPanels, { clientId: 2 }),
    ])

    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([widget('one')])
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 2 })).toEqual([widget('two')])
  })

  it('drops an older answer for the same record that arrives after a newer one', async () => {
    const resolvers: Array<(value: Response) => void> = []
    const waiting = new Promise<Response>((resolve) => resolvers.push(resolve))
    let call = 0
    const fetchMock = (): Promise<Response> => {
      call += 1
      return call === 1 ? waiting : Promise.resolve(json(clientPanels(widget('fresh'))))
    }
    vi.stubGlobal('fetch', vi.fn(fetchMock))
    const store = usePluginSlotsStore()

    const older = store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })
    resolvers[0]?.(json(clientPanels(widget('stale'))))
    await older

    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([widget('fresh')])
  })

  it('leaves the slot empty when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots': () => problem('Client not found.', 404) })
    const store = usePluginSlotsStore()

    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([])
  })

  it('leaves the slot empty when nothing answers', async () => {
    stubOffline()
    const store = usePluginSlotsStore()

    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([])
  })
})

describe('pluginSlotsStore.hide', () => {
  it('adds the widget to the slot setting and drops it from the list', async () => {
    const routes = stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('usage'), widget('other'))),
      'GET /api/admin/settings': () => json([hiddenRow]),
      'PUT /api/admin/settings/5': () => new Response(null, { status: 204 }),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    const hidden = await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('usage'))

    expect(hidden).toBe(true)
    const put = routes.mock.calls.find((call) => call[1]?.method === 'PUT')
    // The service slot already hides this widget, which does not hide it here: the entry names its slot.
    // The entries of the other slot stay as they are.
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({
      value: 'admin.client.panels:sites:old,admin.service.panels:sites:usage,admin.client.panels:sites:usage',
    })
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([widget('other')])
  })

  it('does not write a widget twice when the setting already lists it', async () => {
    const routes = stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('old'), widget('other'))),
      'GET /api/admin/settings': () => json([hiddenRow]),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    const hidden = await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('old'))

    expect(hidden).toBe(true)
    expect(routes.mock.calls.some((call) => call[1]?.method === 'PUT')).toBe(false)
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toEqual([widget('other')])
  })

  it('starts the list when the row is empty', async () => {
    const routes = stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('usage'))),
      'GET /api/admin/settings': () => json([{ ...hiddenRow, value: null, hasValue: false }]),
      'PUT /api/admin/settings/5': () => new Response(null, { status: 204 }),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('usage'))

    const put = routes.mock.calls.find((call) => call[1]?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ value: 'admin.client.panels:sites:usage' })
  })

  it('refuses and keeps the widget when the host never seeded the row', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('usage'))),
      'GET /api/admin/settings': () => json([]),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    const hidden = await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('usage'))

    expect(hidden).toBe(false)
    expect(store.actionError).toContain('plugins.hidden_slot_widgets')
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toHaveLength(1)
  })

  it('keeps the API sentence and the widget when the save is refused', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('usage'))),
      'GET /api/admin/settings': () => json([hiddenRow]),
      'PUT /api/admin/settings/5': () => problem('Value too long.', 400),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    const hidden = await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('usage'))

    expect(hidden).toBe(false)
    expect(store.actionError).toBe('Value too long.')
    expect(store.widgetsFor(PluginSlotId.AdminClientPanels, { clientId: 1 })).toHaveLength(1)
  })

  it('keeps the API sentence when the settings cannot be loaded', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots': () => json(clientPanels(widget('usage'))),
      'GET /api/admin/settings': () => problem('Settings unavailable.', 503),
    })
    const store = usePluginSlotsStore()
    await store.load(PluginSlotId.AdminClientPanels, { clientId: 1 })

    expect(await store.hide(PluginSlotId.AdminClientPanels, { clientId: 1 }, widget('usage'))).toBe(false)
    expect(store.actionError).toBe('Settings unavailable.')
  })
})
