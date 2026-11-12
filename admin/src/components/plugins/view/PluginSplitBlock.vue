<script setup lang="ts">
/**
 * Two panes side by side, the first a bounded column and the second taking the rest; stacked on a phone,
 * the first pane on top. The blocks below know they sit in a pane, so a chat keeps its log bounded.
 */
import PluginBlocks from '@/components/plugins/view/PluginBlocks.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { providePluginPane } from '@/composables/usePluginView'
import type { PluginSplitBlock } from '@/types/pluginview'
import { UiGap, UiSplitRatio } from '@/utils/ui/layout'

/** Props for PluginSplitBlock. */
defineProps<{
  /** The block. */
  block: PluginSplitBlock
}>()

providePluginPane()
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
      <PluginBlocks :blocks="pane.blocks" :scope="`${position}:`" />
    </UiStack>
  </UiSplit>
</template>
