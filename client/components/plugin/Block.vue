<template>
  <component :is="renderer" v-if="renderer" :block="block" />
</template>

<script setup lang="ts">
/**
 * PluginBlock - picks the host component of one plugin block by its kind (`PluginViews.BlockKinds`). A kind this
 * host does not know renders nothing, so a view written for a newer minor schema still shows what this host can;
 * the install check already refuses a kind outside the schema the host accepts.
 */
import type { Component } from 'vue'
import PluginActionBlock from '~/components/plugin/ActionBlock.vue'
import PluginAlertBlock from '~/components/plugin/AlertBlock.vue'
import PluginBadgeBlock from '~/components/plugin/BadgeBlock.vue'
import PluginChatBlock from '~/components/plugin/ChatBlock.vue'
import PluginEmptyBlock from '~/components/plugin/EmptyBlock.vue'
import PluginFormBlock from '~/components/plugin/FormBlock.vue'
import PluginHeaderBlock from '~/components/plugin/HeaderBlock.vue'
import PluginLinkBlock from '~/components/plugin/LinkBlock.vue'
import PluginProgressBlock from '~/components/plugin/ProgressBlock.vue'
import PluginStatBlock from '~/components/plugin/StatBlock.vue'
import PluginTableBlock from '~/components/plugin/TableBlock.vue'
import PluginTabsBlock from '~/components/plugin/TabsBlock.vue'
import PluginTextBlock from '~/components/plugin/TextBlock.vue'
import type { PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** Props of PluginBlock. */
interface Props {
  /** The block. */
  block: PluginBlock
}

/** The props. */
const props = defineProps<Props>()

/** The component of each block kind; exhaustive over the enum, so a new kind fails the type check until drawn. */
const RENDERERS: Readonly<Record<PluginBlockKind, Component>> = {
  [PluginBlockKind.Header]: PluginHeaderBlock,
  [PluginBlockKind.Text]: PluginTextBlock,
  [PluginBlockKind.Stat]: PluginStatBlock,
  [PluginBlockKind.Table]: PluginTableBlock,
  [PluginBlockKind.Form]: PluginFormBlock,
  [PluginBlockKind.Action]: PluginActionBlock,
  [PluginBlockKind.Link]: PluginLinkBlock,
  [PluginBlockKind.Badge]: PluginBadgeBlock,
  [PluginBlockKind.Empty]: PluginEmptyBlock,
  [PluginBlockKind.Alert]: PluginAlertBlock,
  [PluginBlockKind.Tabs]: PluginTabsBlock,
  [PluginBlockKind.Chat]: PluginChatBlock,
  [PluginBlockKind.Progress]: PluginProgressBlock,
}

/** The component of this block, or undefined for an unknown kind (`hasOwn`, so `__proto__` is no kind either). */
const renderer = computed<Component | undefined>(() =>
  Object.hasOwn(RENDERERS, props.block.kind) ? RENDERERS[props.block.kind] : undefined)
</script>
