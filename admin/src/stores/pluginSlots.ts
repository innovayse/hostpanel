/**
 * Pinia store for the plugin widgets shown in the slots of the client and service pages.
 *
 * The only caller of `usePluginSlotsApi`. What is loaded is kept per slot and per record, so two
 * slots on one page, or the same slot for two clients, never share a list: an answer that arrives
 * after the page moved on lands under the key it was asked for and is read by nobody. A refused load
 * leaves the slot empty, because the page owns its own error state and a plugin must never take it
 * down; a refused hide is kept as the API's own sentence in `actionError`. Nothing is thrown.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { usePluginSlotsApi } from '@/composables/apis/usePluginSlotsApi'
import { runAction } from '@/lib/runAction'
import { translate } from '@/lib/translate'
import { useSettingsStore } from '@/stores/settings'
import { contextKey, PluginSlotHiddenSetting, widgetRef } from '@/types/pluginslot'
import type { PluginSlotContext, PluginSlotId, PluginSlotWidgetDto } from '@/types/pluginslot'

/**
 * Names the list a slot holds for one record.
 *
 * @param slotId - Id of the slot.
 * @param context - The slot's context.
 * @returns The key the list is stored under.
 */
const listKey = (slotId: PluginSlotId, context: PluginSlotContext): string => `${slotId}|${contextKey(context)}`

/** Holds the widgets listed for each slot and record, and hides one on the operator's request. */
export const usePluginSlotsStore = defineStore('pluginSlots', () => {
  const api = usePluginSlotsApi()
  const settingsStore = useSettingsStore()

  /** Widgets by slot and record; a key with no entry has not been loaded. */
  const lists = ref<Record<string, PluginSlotWidgetDto[]>>({})

  /** Sentence for the last refused hide, null when none. */
  const actionError = ref<string | null>(null)

  /** Number of the latest load asked for each key; an older answer for the same key is dropped. */
  const latest = new Map<string, number>()

  /**
   * Reads the widgets loaded for a slot and record.
   *
   * @param slotId - Id of the slot.
   * @param context - The slot's context.
   * @returns The widgets, empty until loaded.
   */
  const widgetsFor = (slotId: PluginSlotId, context: PluginSlotContext): PluginSlotWidgetDto[] =>
    lists.value[listKey(slotId, context)] ?? []

  /**
   * Loads the widgets of a slot for a record.
   *
   * @param slotId - Id of the slot.
   * @param context - The slot's context.
   * @returns Resolves when the request is over; a refusal leaves the slot empty.
   */
  const load = async (slotId: PluginSlotId, context: PluginSlotContext): Promise<void> => {
    const key = listKey(slotId, context)
    const ticket = (latest.get(key) ?? 0) + 1
    latest.set(key, ticket)
    // The sentence of a refused load has nowhere to go: the slot shows nothing, and the page owns its error state.
    const refusal = ref<string | null>(null)
    const result = await runAction(refusal, () => api.getSlot(slotId, context))
    // A newer load of the same key owns the list: a slow older answer must not overwrite it.
    if (result.ok && latest.get(key) === ticket) {
      lists.value = { ...lists.value, [key]: result.value }
    }
  }

  /**
   * Hides a widget in its slot: adds it to the slot's hide setting, then drops it from the list.
   *
   * @param slotId - Id of the slot.
   * @param context - The slot's context.
   * @param widget - The widget to hide.
   * @returns True when hidden; false when refused (see `actionError`).
   */
  const hide = async (
    slotId: PluginSlotId,
    context: PluginSlotContext,
    widget: PluginSlotWidgetDto,
  ): Promise<boolean> => {
    actionError.value = null
    if (settingsStore.settings.length === 0) {
      await settingsStore.fetchSettings()
      if (settingsStore.error) {
        actionError.value = settingsStore.error
        return false
      }
    }
    const settingKey = PluginSlotHiddenSetting[slotId]
    const row = settingsStore.settings.find((setting) => setting.key === settingKey)
    if (!row) {
      // The panel cannot create settings, so a host that did not seed the row is told so.
      actionError.value = translate('plugins.pluginSlot.hideNotSeeded', { key: settingKey })
      return false
    }
    const hidden = (row.value ?? '').split(',').filter((entry) => entry !== '')
    // A widget the setting already lists (the list on screen predates the host's filtering, or the
    // operator hid it in another tab) is not written twice: the row stays as it is.
    const alreadyHidden = hidden.includes(widgetRef(widget))
    const saved = alreadyHidden || (await settingsStore.updateSetting(row.id, [...hidden, widgetRef(widget)].join(',')))
    if (!saved) {
      actionError.value = settingsStore.actionError
      return false
    }
    const key = listKey(slotId, context)
    lists.value = { ...lists.value, [key]: widgetsFor(slotId, context).filter((entry) => widgetRef(entry) !== widgetRef(widget)) }
    return true
  }

  return { actionError, widgetsFor, load, hide }
})
