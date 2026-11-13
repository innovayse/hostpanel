<template>
  <div
    class="grid flex-1 grid-cols-1 gap-4 md:min-h-0 md:grid-cols-[minmax(340px,420px)_minmax(0,1fr)] md:grid-rows-[minmax(0,1fr)]"
    data-testid="plugin-split"
  >
    <div
      v-for="(pane, position) in block.panes"
      :key="position"
      class="min-w-0 md:min-h-0 md:overflow-y-auto"
      :data-testid="`plugin-split-pane-${position}`"
    >
      <UiStack gap="md" class="md:h-full">
        <PluginBlocks :blocks="pane.blocks" :scope="`${position}:`" />
      </UiStack>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * PluginSplitBlock: two panes side by side, the conversation's width bounded and the other pane taking the rest;
 * stacked on a phone, the first pane on top. Each pane scrolls on its own on a wide screen.
 *
 * The row is bounded to the block's height and a pane's content to the pane's: with an open-ended row a tall conversation
 * would stretch the pane instead of scrolling inside it, and its composer would leave the screen.
 */
import { providePluginPane } from '~/composables/usePluginView'
import type { PluginSplitBlock } from '~/types/pluginblock'

/** Props of PluginSplitBlock. */
interface Props {
  /** The block. */
  block: PluginSplitBlock
}

/** The props, read in the template. */
defineProps<Props>()

providePluginPane()
</script>
