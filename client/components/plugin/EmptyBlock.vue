<template>
  <UiEmptyState :title="block.title" :description="block.text" data-testid="plugin-empty" />
</template>

<script setup lang="ts">
/**
 * PluginEmptyBlock - a plugin's empty state: a title and an optional sentence, both shown as text.
 */
import type { PluginEmptyBlock } from '~/types/pluginblock'

/** Props of PluginEmptyBlock. */
interface Props {
  /** The block. */
  block: PluginEmptyBlock
}

defineProps<Props>()
</script>
