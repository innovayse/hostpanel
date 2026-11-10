<script setup lang="ts">
/**
 * One action of a chat's result card: a call drawn as an action block, a "try again" that asks the chat to
 * send its last message again, or a checked link. An action with a `visibleWhen` condition shows only
 * while the condition holds, read again whenever a block the chat reads again is refreshed, so a card
 * stops offering what no longer applies. It reports whether it shows, so the row can hide itself when
 * every action is hidden; a hidden action stays mounted, because it reads the condition again.
 */
import { watch } from 'vue'
import PluginActionBlock from '@/components/plugins/view/PluginActionBlock.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import { usePluginCondition } from '@/composables/usePluginView'
import type { PluginChatResultAction } from '@/types/pluginview'
import { UiSize } from '@/utils/ui/size'

/** Props for PluginChatCardAction. */
const props = withDefaults(defineProps<{
  /** The action, as the event reader parsed it. */
  action: PluginChatResultAction
  /** The ids of the blocks the chat reads again after a turn; a refresh of any reads the condition again. */
  refresh?: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled?: boolean
}>(), {
  /**
   * No block is read again by default.
   *
   * @returns An empty list.
   */
  refresh: () => [],
  disabled: false,
})

/** Events emitted by PluginChatCardAction. */
const emit = defineEmits<{
  /** The operator asked to send the conversation's last message again. */
  resend: []
  /** Whether the action shows, told when it mounts and whenever that changes. */
  visibility: [shown: boolean]
}>()

/** Whether the action shows: always without a condition, otherwise only while the condition holds. */
const visible = usePluginCondition(() => props.action.visibleWhen, () => props.refresh)

watch(visible, (shown) => emit('visibility', shown), { immediate: true })
</script>

<template>
  <template v-if="visible">
    <PluginActionBlock v-if="action.block" :block="action.block" />
    <UiButton v-else-if="action.resend" :size="UiSize.Sm" :disabled="disabled" data-testid="plugin-chat-result-resend" @click="emit('resend')">{{ action.label }}</UiButton>
    <UiLink v-else-if="action.link?.external" :href="action.link.href" external data-testid="plugin-chat-result-link">{{ action.label }}</UiLink>
    <UiLink v-else-if="action.link" :to="action.link.href" data-testid="plugin-chat-result-link">{{ action.label }}</UiLink>
  </template>
</template>
