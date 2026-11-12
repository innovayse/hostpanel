<script setup lang="ts">
/**
 * Draws a list of plugin blocks (a view, a tab or a pane) in document order. Consecutive `stat` blocks are laid
 * out together as a grid of tiles and consecutive `link` blocks as a wrapping row of buttons
 * (`groupPluginBlocks`); every other block stands alone. It renders the groups only: the surrounding stack and its
 * gap belong to the caller, so a view, its tabs and its panes all lay a list out the same way.
 */
import { computed } from 'vue'
import PluginBlock from '@/components/plugins/view/PluginBlock.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { groupPluginBlocks } from '@/lib/pluginView'
import { PluginBlockGroupKind } from '@/types/pluginview'
import type { PluginBlock as PluginBlockData, PluginBlockGroup } from '@/types/pluginview'
import { UiDirection, UiGap } from '@/utils/ui/layout'

/** Props for PluginBlocks. */
const props = withDefaults(defineProps<{
  /** The blocks, in document order. */
  blocks: readonly PluginBlockData[]
  /** A prefix that keeps the keys of lists rendered side by side apart, e.g. the open tab's id. */
  scope?: string
}>(), {
  scope: '',
})

/** The blocks in groups, each block keyed by its id or kind and its place. */
const groups = computed<PluginBlockGroup[]>(() => groupPluginBlocks(props.blocks, props.scope))
</script>

<template>
  <template v-for="group in groups" :key="group.key">
    <!-- A run of stats is a grid of tiles that collapses to one column on a phone. -->
    <UiGrid v-if="group.kind === PluginBlockGroupKind.Stats" :columns="3" :gap="UiGap.Sm" data-testid="plugin-stats">
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </UiGrid>
    <!-- A run of links is a row of buttons that wraps onto more lines when it does not fit. -->
    <UiStack v-else-if="group.kind === PluginBlockGroupKind.Links" :direction="UiDirection.Row" :gap="UiGap.Sm" wrap data-testid="plugin-links">
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </UiStack>
    <template v-else>
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </template>
  </template>
</template>
