<script setup lang="ts">
/**
 * Two panes side by side, the first a bounded column and the second taking the rest; stacked on a phone,
 * the first pane on top. The blocks below know they sit in a pane, so a chat keeps its log bounded.
 */
import PluginBlock from '@/components/plugins/view/PluginBlock.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { providePluginPane } from '@/composables/usePluginView'
import type { PluginBlock as PluginBlockData, PluginSplitBlock, PluginSplitPane } from '@/types/pluginview'
import { UiGap, UiSplitRatio } from '@/utils/ui/layout'

/** Props for PluginSplitBlock. */
defineProps<{
  /** The block. */
  block: PluginSplitBlock
}>()

providePluginPane()

/**
 * A pane's blocks with a key each, which a view document never reorders.
 *
 * @param pane - The pane.
 * @param position - The pane's place.
 * @returns The keyed blocks.
 */
const blocksOf = (pane: PluginSplitPane, position: number): { key: string; block: PluginBlockData }[] =>
  pane.blocks.map((block, index) => ({ key: `${position}:${block.id ?? block.kind}-${index}`, block }))
</script>

<template>
  <UiSplit :ratio="UiSplitRatio.Half" :gap="UiGap.Md" data-testid="plugin-split">
    <UiStack
      v-for="(pane, position) in block.panes"
      :key="position"
      :gap="UiGap.Md"
      class="min-w-0"
      :data-testid="`plugin-split-pane-${position}`"
    >
      <PluginBlock v-for="entry in blocksOf(pane, position)" :key="entry.key" :block="entry.block" />
    </UiStack>
  </UiSplit>
</template>
