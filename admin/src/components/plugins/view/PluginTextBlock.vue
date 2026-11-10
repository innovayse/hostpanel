<script setup lang="ts">
/**
 * Plain text from a plugin. Interpolated, never bound as HTML, so markup a plugin writes is shown as the
 * characters it is; line breaks are kept.
 */
import UiText from '@/components/ui/UiText.vue'
import type { PluginTextBlock } from '@/types/pluginview'

/** Props for PluginTextBlock. */
defineProps<{
  /** The block. */
  block: PluginTextBlock
}>()
</script>

<template>
  <UiText class="whitespace-pre-line" data-testid="plugin-text">{{ block.text }}</UiText>
</template>
