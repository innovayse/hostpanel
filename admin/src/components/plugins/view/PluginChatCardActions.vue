<script setup lang="ts">
/**
 * The row of actions under a chat's result card, and its details switch. The row is hidden when no action
 * shows and the card has no details switch, so a card whose offers have all lapsed leaves no empty gap; it
 * is hidden, never removed, because a hidden action is what reads its condition again and brings it back.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginChatCardAction from '@/components/plugins/view/PluginChatCardAction.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import type { PluginChatResult } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'

/** Props for PluginChatCardActions. */
const props = defineProps<{
  /** The result card whose actions the row draws. */
  result: PluginChatResult
  /** The ids of the blocks the chat reads again after a turn. */
  refresh: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled: boolean
}>()

/** Events emitted by PluginChatCardActions. */
const emit = defineEmits<{
  /** An action asked to send the conversation's last message again. */
  resend: []
  /** The operator pressed the details switch. */
  toggleDetails: []
}>()

/** Translation function. */
const { t } = useI18n()

/** The keys of the conditional actions that have reported they show. */
const shownKeys = ref<ReadonlySet<number>>(new Set())

/**
 * Records whether one action shows.
 *
 * @param key - The action's place in the card.
 * @param shown - Whether it shows.
 * @returns Nothing.
 */
const setShown = (key: number, shown: boolean): void => {
  const next = new Set(shownKeys.value)
  if (shown) {
    next.add(key)
  } else {
    next.delete(key)
  }
  shownKeys.value = next
}

/** Whether the row shows: the details switch always does, an action with no condition always does. */
const shown = computed<boolean>(() =>
  props.result.details.length > 0
  || props.result.actions.some((action) => !action.visibleWhen || shownKeys.value.has(action.key)))
</script>

<template>
  <UiStack v-show="shown" direction="row" :gap="UiGap.Sm" :align="UiAlignItems.Center" wrap data-testid="plugin-chat-result-actions">
    <PluginChatCardAction
      v-for="action in result.actions"
      :key="action.key"
      :action="action"
      :refresh="refresh"
      :disabled="disabled"
      @resend="emit('resend')"
      @visibility="setShown(action.key, $event)"
    />
    <UiButton
      v-if="result.details.length > 0"
      :variant="UiButtonVariant.Secondary"
      :size="UiSize.Sm"
      data-testid="plugin-chat-result-details-toggle"
      @click="emit('toggleDetails')"
    >{{ t('plugins.pluginPage.chatResultDetails') }}</UiButton>
  </UiStack>
</template>
