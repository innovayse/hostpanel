/**
 * The plugin widgets of one host slot, read after the host page has mounted: a page never waits
 * for its slots (and on the server nothing is read, so a slow plugin never delays the first
 * render), and a slot that cannot be read shows nothing.
 *
 * @module composables/usePluginSlot
 */

import type { MaybeRefOrGetter, Ref } from 'vue'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSlotId } from '~/types/pluginslotid'
import type { PluginSlotWidget } from '~/types/pluginslotwidget'

/**
 * Reads a slot's widgets once the calling component is mounted, and again when the slot or the context changes.
 *
 * @param slotId - The slot id, or a getter of it (a component's prop), read again on every load.
 * @param context - The slot's context, e.g. `{ serviceId: '4' }`.
 * @returns The widgets, empty until read, on a failure and with no plugin.
 */
export const usePluginSlot = (
  slotId: MaybeRefOrGetter<PluginSlotId>,
  context: () => Record<string, string> = () => ({})
): { widgets: Ref<PluginSlotWidget[]> } => {
  const store = usePluginsStore()

  /** The widgets read. */
  const widgets = ref<PluginSlotWidget[]>([])

  /** The number of the latest read; a reply to an older one is dropped. */
  let latest = 0

  /**
   * Reads the widgets for the current context.
   *
   * @returns Resolves when the read has ended.
   */
  const load = async (): Promise<void> => {
    latest += 1
    const read = latest
    // The context is cleared at once: the old service's tabs must never show beside the new one.
    widgets.value = []
    const listed = await store.loadSlot(toValue(slotId), context())
    // Two quick context changes can answer out of order; only the reply to the latest read is kept.
    if (read === latest) {
      widgets.value = listed
    }
  }

  // onMounted runs in the browser only, which is what keeps slot reads off the server render.
  onMounted(load)
  watch(() => [toValue(slotId), context()] as const, load, { deep: true })

  return { widgets }
}
