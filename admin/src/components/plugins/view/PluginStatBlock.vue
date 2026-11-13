<script setup lang="ts">
/**
 * A labelled value, written in the view or read from a GET route, formatted with the operator's locale
 * and shown as text in a tile. A failed read shows the API's sentence in place of the value, so one failing block
 * never hides the rest of the page. `format: badge` draws the word as a badge in the tile.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import { usePluginSource } from '@/composables/usePluginView'
import { formatPluginValue } from '@/lib/pluginView'
import { PluginValueFormat } from '@/types/pluginview'
import type { PluginStatBlock } from '@/types/pluginview'

/** Props for PluginStatBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginStatBlock
}>()

/** The operator's locale. */
const { locale } = useI18n()

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** The value as text. */
const formatted = computed<string>(() =>
  formatPluginValue(props.block.source ? value.value : props.block.value, props.block.format, locale.value))

/** Whether the value is drawn as a badge: asked for by the view and read without a failure. */
const badge = computed<boolean>(() => props.block.format === PluginValueFormat.Badge && !error.value)
</script>

<template>
  <!-- The tile keeps its place while the source is read. A status word is drawn as a badge in the tile; a failed read still shows the sentence as a plain value. -->
  <UiStatTile :label="block.label" :value="error || formatted" :loading="loading" data-testid="plugin-stat">
    <template v-if="badge" #value>
      <UiBadge data-testid="plugin-stat-badge">{{ formatted }}</UiBadge>
    </template>
  </UiStatTile>
</template>
