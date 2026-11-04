/**
 * Tests for the plugin slots API composable: path, query and the index object, through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { usePluginSlotsApi } from '@/composables/apis/usePluginSlotsApi'
import { PluginSlotId } from '@/types/pluginslot'
import { json, problem, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('usePluginSlotsApi', () => {
  it('getSlot reads the slot with the client in the query', async () => {
    const usage = { pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Usage', order: 1, contextKeys: ['clientId'] }
    const mock = stubFetch(json({ slotId: 'admin.client.panels', widgets: [usage] }))

    const index = await usePluginSlotsApi().getSlot(PluginSlotId.AdminClientPanels, { clientId: 4 })

    expect(index).toEqual({ slotId: 'admin.client.panels', widgets: [usage] })
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/plugins/slots/admin.client.panels?clientId=4')
  })

  it('getSlot reads the slot with the service in the query', async () => {
    const mock = stubFetch(json({ slotId: 'admin.service.panels', widgets: [] }))

    await usePluginSlotsApi().getSlot(PluginSlotId.AdminServicePanels, { serviceId: 9 })

    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/plugins/slots/admin.service.panels?serviceId=9')
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Not found.', 404))

    await expect(usePluginSlotsApi().getSlot(PluginSlotId.AdminClientPanels, { clientId: 1 })).rejects.toBeTruthy()
  })
})
