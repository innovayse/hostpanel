<template>
  <UiToneCard :tone="pluginUiTone(result.tone)" data-testid="plugin-chat-result">
    <UiStack :gap="UiGap.Xs">
      <UiText v-if="result.title" as="p" :weight="UiTextWeight.Semibold" data-testid="plugin-chat-result-title">{{ result.title }}</UiText>
      <UiText v-if="result.text" as="p" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-chat-result-text">{{ result.text }}</UiText>
      <PluginChatCardActions
        v-if="result.actions.length || result.details.length"
        :result="result"
        :refresh="refresh"
        :disabled="disabled"
        @resend="emit('resend')"
        @toggle-details="open = !open"
      />
      <UiStack v-if="open" as="ul" :gap="UiGap.Xs" data-testid="plugin-chat-result-details">
        <UiText v-for="line in result.details" :key="line" as="li" :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ line }}</UiText>
      </UiStack>
    </UiStack>
  </UiToneCard>
</template>

<script setup lang="ts">
/**
 * PluginChatResultCard - one result card of a plugin's reply: a tone edge, a title and a sentence, the card's actions,
 * and the detail lines behind a switch the card keeps open or closed itself.
 */
import { UiGap, UiTextSize, UiTextTone, UiTextWeight } from '~/utils/ui/enums'
import type { PluginChatResult } from '~/types/pluginchatresult'
import { pluginUiTone } from '~/utils/plugins'

/** Props of PluginChatResultCard. */
interface Props {
  /** The card. */
  result: PluginChatResult
  /** The ids of the blocks the chat reads again after a turn; passed on to the card's actions. */
  refresh: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled: boolean
}

/** The props. */
defineProps<Props>()

/** The events of PluginChatResultCard. */
const emit = defineEmits<{
  /** An action asked to send the conversation's last message again. */
  resend: []
}>()

/** Whether the detail lines show. */
const open = ref(false)
</script>
