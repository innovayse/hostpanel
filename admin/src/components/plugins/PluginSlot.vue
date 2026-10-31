<script setup lang="ts">
/**
 * A named slot on an admin page: lists the plugin widgets the host offers for one client or
 * service and gives each its own {@link PluginWidget}.
 *
 * It renders nothing at all while there is nothing to show: no plugins, a slot nobody fills, a
 * load still running or a refused load (the page owns its own error state, and a plugin must
 * never take the page down). The page keys the slot by its record, so another client or service
 * mounts a fresh slot, and the store keeps each record's list apart besides.
 * The body of a widget comes through the `widget` scoped slot.
 */
import { computed, onMounted, watch } from 'vue'
import PluginWidget from '@/components/plugins/PluginWidget.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { usePluginSlotsStore } from '@/stores/pluginSlots'
import { UiTone } from '@/utils/ui/tone'
import { contextKey } from '@/types/pluginslot'
import type { PluginSlotContext, PluginSlotId, PluginSlotWidgetDto } from '@/types/pluginslot'

/** Props for PluginSlot. */
const props = defineProps<{
  /** Id of the slot. */
  slotId: PluginSlotId
  /** The client or service the page shows. */
  context: PluginSlotContext
}>()

defineSlots<{
  /** Body of one widget; rendered inside that widget's own failure boundary. */
  widget(props: { widget: PluginSlotWidgetDto }): unknown
}>()

/** Slot store: the widgets per slot and record. */
const store = usePluginSlotsStore()

/** The widgets listed for this slot and record. */
const widgets = computed<PluginSlotWidgetDto[]>(() => store.widgetsFor(props.slotId, props.context))

/**
 * Hides one widget at the operator's request.
 *
 * @param widget - The widget to hide.
 * @returns Resolves when the request is over; a refusal is left in the store's `actionError`.
 */
const hide = async (widget: PluginSlotWidgetDto): Promise<void> => {
  await store.hide(props.slotId, props.context, widget)
}

onMounted(() => store.load(props.slotId, props.context))

watch(() => [props.slotId, contextKey(props.context)], () => store.load(props.slotId, props.context))
</script>

<template>
  <UiStack v-if="widgets.length > 0" :data-testid="`plugin-slot-${slotId}`">
    <UiStatusMessage :message="store.actionError" :tone="UiTone.Danger" data-testid="plugin-slot-error" />
    <PluginWidget
      v-for="widget in widgets"
      :key="`${widget.pluginId}:${widget.widgetId}`"
      :widget="widget"
      @hide="hide(widget)"
    >
      <slot name="widget" :widget="widget" />
    </PluginWidget>
  </UiStack>
</template>
