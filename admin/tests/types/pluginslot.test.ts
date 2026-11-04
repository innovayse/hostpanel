/**
 * Unit tests for the plugin slot types: the stable slot ids, the one hide setting, the
 * `slot:plugin:widget` entries it holds and the names that key a record.
 */
import { describe, expect, it } from 'vitest'
import { contextKey, HIDDEN_SLOT_WIDGETS_SETTING, hiddenWidgetRefs, PluginSlotId, widgetRef } from '@/types/pluginslot'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'

/** A widget as the host lists it. */
const usage: PluginSlotWidgetDto = {
  pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Usage', order: 1, contextKeys: ['clientId'],
}

describe('PluginSlotId', () => {
  it('holds the ids the SDK publishes for the admin', () => {
    expect(Object.values(PluginSlotId)).toEqual(['admin.client.panels', 'admin.service.panels'])
  })
})

describe('HIDDEN_SLOT_WIDGETS_SETTING', () => {
  it('is the one row the host seeds for the hidden widgets of every slot', () => {
    expect(HIDDEN_SLOT_WIDGETS_SETTING).toBe('plugins.hidden_slot_widgets')
  })
})

describe('widgetRef', () => {
  it('names the slot, the plugin and the widget, as the host writes an entry', () => {
    expect(widgetRef(PluginSlotId.AdminClientPanels, usage)).toBe('admin.client.panels:sites:usage')
  })

  it('names the same widget differently in another slot', () => {
    expect(widgetRef(PluginSlotId.AdminServicePanels, usage)).not.toBe(widgetRef(PluginSlotId.AdminClientPanels, usage))
  })
})

describe('hiddenWidgetRefs', () => {
  it('reads an empty row as no entries', () => {
    expect(hiddenWidgetRefs(null)).toEqual([])
    expect(hiddenWidgetRefs('')).toEqual([])
  })

  it('trims entries and drops empty ones, the way the host reads them', () => {
    expect(hiddenWidgetRefs(' admin.client.panels:a:b ,, admin.service.panels:c:d,')).toEqual([
      'admin.client.panels:a:b',
      'admin.service.panels:c:d',
    ])
  })
})

describe('contextKey', () => {
  it('separates a client from a service with the same number', () => {
    expect(contextKey({ clientId: 7 })).toBe('client:7')
    expect(contextKey({ serviceId: 7 })).toBe('service:7')
  })
})
