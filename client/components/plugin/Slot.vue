<script setup lang="ts">
/**
 * PluginSlot: the cards plugins add to a named place on a host page, each an isolated widget. The
 * index is read after the page mounts and shows nothing at all with no plugin or when it cannot be
 * read, so the page looks exactly as it does without plugins.
 */
import { usePluginSlot } from '~/composables/usePluginSlot'
import type { PluginSlotId } from '~/types/pluginslotid'

/** Props of PluginSlot. */
interface Props {
  /** The slot id. */
  slotId: PluginSlotId
  /** The slot's context, e.g. `{ serviceId: '4' }`. */
  context?: Record<string, string>
}

/** The props. */
const props = withDefaults(defineProps<Props>(), { context: () => ({}) })

/** The widgets. */
const { widgets } = usePluginSlot(() => props.slotId, () => props.context)

/** A key per widget and context, so a widget of another context is a new, freshly isolated instance. */
const contextKey = computed(() => JSON.stringify(props.context))
</script>

<template>
  <UiGrid v-if="widgets.length" cols="2" gap="lg" :data-testid="`plugin-slot-${slotId}`">
    <UiCard v-for="widget in widgets" :key="`${widget.pluginId}/${widget.id}:${contextKey}`" :title="widget.title ?? undefined">
      <PluginSlotWidget :slot-id="slotId" :widget="widget" :context="context" />
    </UiCard>
  </UiGrid>
</template>
