<script setup lang="ts">
/**
 * A plugin view's title, with its actions beside it. The title is interpolated, never bound as HTML;
 * `titleSource` and `subtitleSource` read it, and a smaller line under it, from GET routes. Each action is
 * drawn through PluginBlock, so its own `visibleWhen` applies like any block's.
 *
 * The sentence an action answers with is shown once, on one line under the whole header, so a sentence
 * never grows one button's column and the row stays aligned; the latest replaces the previous one. A
 * `compact` header is one slim line: the title, the smaller line and the actions on the right.
 */
import { computed } from 'vue'
import PluginBlock from '@/components/plugins/view/PluginBlock.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { providePluginActionMessages, usePluginSource } from '@/composables/usePluginView'
import type { PluginBlock as PluginBlockData, PluginHeaderBlock } from '@/types/pluginview'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Props for PluginHeaderBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginHeaderBlock
}>()

/** The title read, when the view names a source for it. */
const { value: titleRead } = usePluginSource(() => props.block.titleSource, () => props.block.id)

/** The smaller line read, when the view names a source for it. */
const { value: subtitleRead } = usePluginSource(() => props.block.subtitleSource, () => props.block.id)

/** The last sentence one of the header's actions answered with; null until one has. */
const message = providePluginActionMessages()

/** The title shown: what was read, else what the view wrote. */
const title = computed<string>(() => (typeof titleRead.value === 'string' && titleRead.value !== '' ? titleRead.value : props.block.title ?? ''))

/** The smaller line shown; empty for none. */
const subtitle = computed<string>(() => (typeof subtitleRead.value === 'string' ? subtitleRead.value : ''))

/** The tone of the action sentence. */
const messageTone = computed<UiTone>(() => (message.value?.failed ? UiTone.Danger : UiTone.Success))

/** The actions, keyed by id or kind and their place. */
const actions = computed<{ key: string; block: PluginBlockData }[]>(() =>
  (props.block.actions ?? []).map((action, position) => ({ key: `${action.id ?? action.kind}:${position}`, block: action })))
</script>

<template>
  <UiStack :gap="UiGap.Xs" data-testid="plugin-header-block">
    <UiStack v-if="block.compact" direction="row" :gap="UiGap.Md" :align="UiAlignItems.Center" wrap data-testid="plugin-header-compact">
      <UiStack v-if="title || subtitle" :gap="UiGap.None" class="min-w-0">
        <UiHeading :level="2" :variant="UiHeadingVariant.Card" class="truncate" data-testid="plugin-header">{{ title }}</UiHeading>
        <UiText v-if="subtitle" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="truncate font-mono" data-testid="plugin-header-subtitle">{{ subtitle }}</UiText>
      </UiStack>
      <UiStack direction="row" :gap="UiGap.Md" :align="UiAlignItems.Center" wrap class="ml-auto">
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </UiStack>
    </UiStack>
    <UiPageHeader v-else :title="title" :subtitle="subtitle || undefined" data-testid="plugin-header">
      <template v-if="actions.length > 0" #actions>
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </template>
    </UiPageHeader>
    <UiStatusMessage :message="message?.text || null" :tone="messageTone" data-testid="plugin-header-message" />
  </UiStack>
</template>
