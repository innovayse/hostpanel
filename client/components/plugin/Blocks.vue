<template>
  <template v-for="group in groups" :key="group.key">
    <!-- A run of stats is a grid of tiles that follows the width it has. -->
    <UiGrid v-if="group.kind === PluginBlockGroupKind.Stats" :cols="UiGridColumns.Auto" gap="sm" data-testid="plugin-stats">
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </UiGrid>
    <!-- A run of links is a row of buttons that wraps onto more lines when it does not fit. -->
    <UiStack v-else-if="group.kind === PluginBlockGroupKind.Links" direction="row" gap="sm" wrap data-testid="plugin-links">
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </UiStack>
    <template v-else>
      <PluginBlock v-for="entry in group.blocks" :key="entry.key" :block="entry.block" />
    </template>
  </template>
</template>

<script setup lang="ts">
/**
 * PluginBlocks - draws a list of plugin blocks (a view, a tab or a pane) in document order. Consecutive `stat` blocks
 * are laid out together as a grid of tiles and consecutive `link` blocks as a wrapping row of buttons
 * (`groupPluginBlocks`); every other block stands alone. It renders the groups only: the surrounding stack and its
 * gap belong to the caller, so a view, its tabs and its panes all lay a list out the same way.
 */
import type { PluginBlock as PluginBlockData } from '~/types/pluginblock'
import { PluginBlockGroupKind } from '~/types/pluginblockkind'
import { UiGridColumns } from '~/utils/ui/enums'
import { groupPluginBlocks } from '~/utils/plugins'

/** Props of PluginBlocks. */
interface Props {
  /** The blocks, in document order. */
  blocks: PluginBlockData[]
  /** A prefix that keeps the keys of lists rendered side by side apart, e.g. the open tab's id. */
  scope?: string
}

/** The props. */
const props = withDefaults(defineProps<Props>(), { scope: '' })

/** The blocks in groups, each block keyed by its id or kind and its place. */
const groups = computed(() => groupPluginBlocks(props.blocks, props.scope))
</script>
