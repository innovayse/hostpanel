<script setup lang="ts">
/**
 * One result card of a plugin's reply: a tone, a title and a sentence, the card's actions, and the detail
 * lines behind a switch the card keeps open or closed itself. Everything is text.
 */
import { ref } from 'vue'
import PluginChatCardActions from '@/components/plugins/view/PluginChatCardActions.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { pluginAlertTone } from '@/lib/pluginView'
import type { PluginChatResult } from '@/types/pluginview'
import { UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for PluginChatResultCard. */
defineProps<{
  /** The card. */
  result: PluginChatResult
  /** The ids of the blocks the chat reads again after a turn; passed on to the card's actions. */
  refresh: readonly string[]
  /** Whether a "try again" is disabled, as while a reply streams. */
  disabled: boolean
}>()

/** Events emitted by PluginChatResultCard. */
const emit = defineEmits<{
  /** An action asked to send the conversation's last message again. */
  resend: []
}>()

/** Whether the detail lines show. */
const open = ref(false)
</script>

<template>
  <UiAlert :variant="pluginAlertTone(result.tone)" data-testid="plugin-chat-result">
    <UiStack :gap="UiGap.Xs">
      <UiText v-if="result.title" class="font-semibold" data-testid="plugin-chat-result-title">{{ result.title }}</UiText>
      <UiText v-if="result.text" :size="UiTextSize.Sm" data-testid="plugin-chat-result-text">{{ result.text }}</UiText>
      <PluginChatCardActions
        v-if="result.actions.length > 0 || result.details.length > 0"
        :result="result"
        :refresh="refresh"
        :disabled="disabled"
        @resend="emit('resend')"
        @toggle-details="open = !open"
      />
      <UiStack v-if="open" :gap="UiGap.Xs" data-testid="plugin-chat-result-details">
        <UiText v-for="line in result.details" :key="line" :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ line }}</UiText>
      </UiStack>
    </UiStack>
  </UiAlert>
</template>
