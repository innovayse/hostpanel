<script setup lang="ts">
/**
 * Tabs, each holding blocks. Only the open tab's blocks are rendered, so a closed tab reads no route;
 * opening it mounts its blocks, which read then. The optional `actions` sit on the tab row, whichever tab
 * is open.
 */
import { computed, ref } from 'vue'
import PluginActionBlock from '@/components/plugins/view/PluginActionBlock.vue'
import PluginBlocks from '@/components/plugins/view/PluginBlocks.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import type { PluginBlock as PluginBlockData, PluginTabsBlock } from '@/types/pluginview'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Props for PluginTabsBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginTabsBlock
}>()

/** The open tab's id; the first tab to begin with. */
const active = ref<string>(props.block.tabs[0]?.id ?? '')

/** The tabs as the kit draws them; the titles are text. */
const tabs = computed<UiTabItem[]>(() =>
  props.block.tabs.map((tab) => ({ value: tab.id, label: tab.title, testid: `plugin-tab-${tab.id}` })))

/** The open tab's blocks; the tab id scopes their keys, so switching tabs mounts fresh blocks. */
const openBlocks = computed<readonly PluginBlockData[]>(() => props.block.tabs.find((tab) => tab.id === active.value)?.blocks ?? [])
</script>

<template>
  <UiStack :gap="UiGap.Md" data-testid="plugin-tabs">
    <UiStack direction="row" :gap="UiGap.Sm" :align="UiAlignItems.Center">
      <UiTabs v-model="active" :items="tabs" class="min-w-0 flex-1" />
      <PluginActionBlock v-for="(action, position) in block.actions ?? []" :key="`${position}-${action.label}`" :block="action" />
    </UiStack>
    <PluginBlocks :blocks="openBlocks" :scope="`${active}:`" />
  </UiStack>
</template>
