<script setup lang="ts">
/**
 * A plugin's empty state: a title and an optional sentence, both shown as text.
 */
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import type { PluginEmptyBlock } from '@/types/pluginview'

/** Props for PluginEmptyBlock. */
defineProps<{
  /** The block. */
  block: PluginEmptyBlock
}>()
</script>

<template>
  <UiEmptyState :title="block.title" :description="block.text" data-testid="plugin-empty" />
</template>
