<template>
  <UiSkeleton v-if="loading" shape="block" class="h-16" />
  <!-- A status word is drawn as a badge above its label; a failed read still shows the sentence as a plain value. -->
  <UiStack v-else-if="badge" gap="sm" data-testid="plugin-stat">
    <UiBadge size="sm" data-testid="plugin-stat-badge">{{ formatted }}</UiBadge>
    <UiText as="p" size="sm" tone="muted">{{ block.label }}</UiText>
  </UiStack>
  <UiStat v-else :label="block.label" :value="error || formatted" data-testid="plugin-stat" />
</template>

<script setup lang="ts">
/**
 * PluginStatBlock - a labelled value, written in the view or read from a GET route, formatted with the host's
 * locale formatters and shown as text. A failed read shows the store's sentence (the API's `detail`) in place of
 * the value, so one failing block never hides the rest of the page. `format: badge` draws the word as a badge.
 */
import { usePluginSource } from '~/composables/usePluginView'
import type { PluginStatBlock } from '~/types/pluginblock'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import { formatPluginValue } from '~/utils/plugins'

/** Props of PluginStatBlock. */
interface Props {
  /** The block. */
  block: PluginStatBlock
}

/** The props. */
const props = defineProps<Props>()

/** The reader's locale. */
const { locale } = useI18n()

/** The source read, when the block names one. */
const { value, loading, error } = usePluginSource(() => props.block.source, () => props.block.id)

/** Whether the value is drawn as a badge: asked for by the view, read without a failure and not empty. */
const badge = computed(() => props.block.format === PluginValueFormat.Badge && !error.value && formatted.value !== '')

/** The value as text. */
const formatted = computed(() =>
  formatPluginValue(props.block.source ? value.value : props.block.value, props.block.format, locale.value))
</script>
