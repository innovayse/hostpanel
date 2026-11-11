<script setup lang="ts">
/**
 * Picks the component of one plugin block by its kind. A kind this panel does not draw renders nothing:
 * a view written for a newer minor schema still shows what the panel can, and `preview` is refused on an
 * admin page at install. A block with `visibleWhen` shows only while its condition holds, read again
 * whenever its own id is refreshed.
 */
import { computed } from 'vue'
import type { Component } from 'vue'
import PluginActionBlock from '@/components/plugins/view/PluginActionBlock.vue'
import PluginAlertBlock from '@/components/plugins/view/PluginAlertBlock.vue'
import PluginBadgeBlock from '@/components/plugins/view/PluginBadgeBlock.vue'
import PluginChatBlock from '@/components/plugins/view/PluginChatBlock.vue'
import PluginEmptyBlock from '@/components/plugins/view/PluginEmptyBlock.vue'
import PluginFormBlock from '@/components/plugins/view/PluginFormBlock.vue'
import PluginHeaderBlock from '@/components/plugins/view/PluginHeaderBlock.vue'
import PluginLinkBlock from '@/components/plugins/view/PluginLinkBlock.vue'
import PluginProgressBlock from '@/components/plugins/view/PluginProgressBlock.vue'
import PluginSplitBlock from '@/components/plugins/view/PluginSplitBlock.vue'
import PluginStatBlock from '@/components/plugins/view/PluginStatBlock.vue'
import PluginTableBlock from '@/components/plugins/view/PluginTableBlock.vue'
import PluginTabsBlock from '@/components/plugins/view/PluginTabsBlock.vue'
import PluginTextBlock from '@/components/plugins/view/PluginTextBlock.vue'
import { usePluginCondition } from '@/composables/usePluginView'
import { PluginBlockKind } from '@/types/pluginview'
import type { PluginBlock } from '@/types/pluginview'

/** Props for PluginBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginBlock
}>()

/** The kinds the admin panel draws: every kind but `preview`, which only a client page may hold. */
type AdminBlockKind = Exclude<PluginBlockKind, PluginBlockKind.Preview>

/** The component of each kind the admin panel draws; exhaustive, so a new kind fails the type check until drawn. */
const RENDERERS: Readonly<Record<AdminBlockKind, Component>> = {
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
  [PluginBlockKind.Split]: PluginSplitBlock,
}

/** Whether the block shows: always without a condition, otherwise only while its condition holds. */
const visible = usePluginCondition(() => props.block.visibleWhen, () => (props.block.id ? [props.block.id] : []))

/** The component of this block, or undefined for a kind not drawn here (`hasOwn`, so `__proto__` is no kind either). */
const renderer = computed<Component | undefined>(() =>
  Object.hasOwn(RENDERERS, props.block.kind) ? RENDERERS[props.block.kind as AdminBlockKind] : undefined)
</script>

<template>
  <component :is="renderer" v-if="renderer && visible" :block="block" />
</template>
