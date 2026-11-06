<template>
  <UiStack gap="md" :class="inPane ? 'flex-1 md:min-h-0' : ''" data-testid="plugin-tabs">
    <UiStack direction="row" gap="sm" align="center">
      <UiTabs v-model="active" :tabs="tabs" class="min-w-0 flex-1" />
      <PluginActionBlock v-for="(action, position) in block.actions" :key="`${position}-${action.label}`" :block="action" />
    </UiStack>
    <PluginBlock v-for="entry in blocks" :key="entry.key" :block="entry.block" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginTabsBlock - tabs, each holding blocks. Only the open tab's blocks are rendered, so a closed tab reads no
 * route; opening it mounts its blocks, which read then. The optional `actions` sit on the tab row, whichever tab is open.
 * In a split pane the tabs pass the pane's height on, so a chat in a tab keeps its composer in view.
 */
import PluginActionBlock from '~/components/plugin/ActionBlock.vue'
import { usePluginPane } from '~/composables/usePluginView'
import type { PluginTabsBlock } from '~/types/pluginblock'

/** Props of PluginTabsBlock. */
interface Props {
  /** The block. */
  block: PluginTabsBlock
}

/** The props. */
const props = defineProps<Props>()

/** Whether the tabs sit in a split pane, which gives them their height on a wide screen. */
const inPane = usePluginPane()

/** The open tab's id; the first tab to begin with. */
const active = ref(props.block.tabs[0]?.id ?? '')

/** The tabs as the kit draws them; the titles are text. */
const tabs = computed(() => props.block.tabs.map(tab => ({ value: tab.id, label: tab.title })))

/** The open tab's blocks, keyed by the tab as well, so switching tabs mounts fresh blocks. */
const blocks = computed(() => (props.block.tabs.find(tab => tab.id === active.value)?.blocks ?? [])
  .map((block, position) => ({ key: `${active.value}:${block.id ?? `${block.kind}-${position}`}`, block })))
</script>
