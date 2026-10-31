/**
 * Unit tests for the plugin slot types: the stable slot ids, the hide setting of each slot and the
 * names that key a widget and a record.
 */
import { describe, expect, it } from 'vitest'
import { contextKey, PluginSlotHiddenSetting, PluginSlotId, widgetRef } from '@/types/pluginslot'

describe('PluginSlotId', () => {
  it('holds the ids the SDK publishes for the admin', () => {
    expect(Object.values(PluginSlotId)).toEqual(['admin.client.panels', 'admin.service.panels'])
  })
})

describe('PluginSlotHiddenSetting', () => {
  it('names a distinct setting row for every slot', () => {
    const keys = Object.values(PluginSlotId).map((id) => PluginSlotHiddenSetting[id])

    expect(new Set(keys).size).toBe(keys.length)
    expect(keys).toEqual([
      'plugins.slots.admin.client.panels.hidden',
      'plugins.slots.admin.service.panels.hidden',
    ])
  })
})

describe('widgetRef', () => {
  it('joins the plugin and the widget id', () => {
    expect(widgetRef({ pluginId: 'sites', widgetId: 'usage', label: 'Usage', order: 1 })).toBe('sites:usage')
  })
})

describe('contextKey', () => {
  it('separates a client from a service with the same number', () => {
    expect(contextKey({ clientId: 7 })).toBe('client:7')
    expect(contextKey({ serviceId: 7 })).toBe('service:7')
  })
})
