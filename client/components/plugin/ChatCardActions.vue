<template>
  <UiStack v-show="shown" :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiCrossAlign.Center" wrap data-testid="plugin-chat-result-actions">
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
      v-if="result.details.length"
      :variant="UiButtonVariant.Outline"
      :size="UiSize.Sm"
      data-testid="plugin-chat-result-details-toggle"
      @click="emit('toggleDetails')"
    >{{ $t('plugins.chatResultDetails') }}</UiButton>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginChatCardActions: the row of actions under a chat's result card, and its details switch.
 *
 * An action may hide itself by its `visibleWhen` condition (PluginChatCardAction). The row is hidden when no action
 * shows and the card has no details switch, so a card whose offers have all lapsed leaves no empty gap between its text
 * and the next message. The row is hidden, never removed: a hidden action must stay mounted, because it is what reads its
 * condition again at the next refresh and brings the row back.
 */
import { UiButtonVariant, UiCrossAlign, UiDirection, UiGap, UiSize } from '~/utils/ui/enums'
import type { PluginChatResult } from '~/types/pluginchatresult'

/** Props of PluginChatCardActions. */
interface Props {
  /** The result card whose actions the row draws. */
  result: PluginChatResult
  /** The ids of the blocks the chat reads again after a turn; passed on to each action's condition. */
  refresh: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled: boolean
}

/** The props. */
const props = defineProps<Props>()

/** The events of PluginChatCardActions. */
const emit = defineEmits<{
  /** An action asked to send the conversation's last message again. */
  resend: []
  /** The reader pressed the details switch. */
  toggleDetails: []
}>()

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

/**
 * Whether the row shows: the details switch always does, an action with no condition always does, and one with a
 * condition does once it has reported that it shows. An action with no condition is not waited for, so the row never
 * flashes hidden before it reports.
 */
const shown = computed(() =>
  props.result.details.length > 0
  || props.result.actions.some(action => !action.visibleWhen || shownKeys.value.has(action.key)))
</script>
