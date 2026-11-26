<script setup lang="ts">
/**
 * The body of one widget in a slot: reads the widget's view document and renders it with the same
 * plugin view renderer a plugin page uses.
 *
 * The component is async (top-level `await`), so it lives under the `<Suspense>` of {@link PluginWidget},
 * which shows the skeleton while it loads. A view the host refused throws, which `PluginWidget` stops
 * and answers with its own neutral note, so one widget never takes the page down. The record the slot
 * is opened for fills the view's route `{param}` segments (`/services/{serviceId}`) and a service page's
 * id also travels as the `serviceId` query of every route call, so the host binds the call to its client.
 */
import PluginView from '@/components/plugins/view/PluginView.vue'
import { usePluginSlotsStore } from '@/stores/pluginSlots'
import { contextCallQuery, contextValues } from '@/types/pluginslot'
import type { PluginSlotContext, PluginSlotId, PluginSlotWidgetDto } from '@/types/pluginslot'

/** Props for PluginSlotWidgetBody. */
const props = defineProps<{
  /** Id of the slot the widget sits in. */
  slotId: PluginSlotId
  /** The client or service the page shows. */
  context: PluginSlotContext
  /** The widget, as the slot lists it. */
  widget: PluginSlotWidgetDto
}>()

/** Slot store, which reads the widget's view. */
const store = usePluginSlotsStore()

/** The widget with its view; the read is awaited before the first render. */
const loaded = await store.loadWidget(props.slotId, props.context, props.widget)

if (loaded === null) {
  // Caught by PluginWidget's failure boundary, which shows its own note; the sentence is not shown to anyone.
  throw new Error(`The view of widget ${props.widget.pluginId}/${props.widget.id} could not be read.`)
}
</script>

<template>
  <!-- A refused view throws in setup; Vue may still draw once without it, so nothing is drawn then -->
  <PluginView
    v-if="loaded"
    :plugin-id="loaded.pluginId"
    :view="loaded.view"
    :values="contextValues(context)"
    :query="contextCallQuery(context)"
    :external-origins="loaded.externalOrigins"
  />
</template>
