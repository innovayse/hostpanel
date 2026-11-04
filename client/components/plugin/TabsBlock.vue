<template>
  <UiStack gap="md" data-testid="plugin-tabs">
    <UiTabs v-model="active" :tabs="tabs" />
    <PluginBlock v-for="entry in blocks" :key="entry.key" :block="entry.block" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginTabsBlock - tabs, each holding blocks. Only the open tab's blocks are rendered, so a closed tab reads no
 * route; opening it mounts its blocks, which read then.
 */
import type { PluginTabsBlock } from '~/types/pluginblock'

/** Props of PluginTabsBlock. */
interface Props {
  /** The block. */
  block: PluginTabsBlock
}

/** The props. */
const props = defineProps<Props>()

/** The open tab's id; the first tab to begin with. */
const active = ref(props.block.tabs[0]?.id ?? '')

/** The tabs as the kit draws them; the titles are text. */
const tabs = computed(() => props.block.tabs.map(tab => ({ value: tab.id, label: tab.title })))

/** The open tab's blocks, keyed by the tab as well, so switching tabs mounts fresh blocks. */
const blocks = computed(() => (props.block.tabs.find(tab => tab.id === active.value)?.blocks ?? [])
  .map((block, position) => ({ key: `${active.value}:${block.id ?? `${block.kind}-${position}`}`, block })))
</script>
