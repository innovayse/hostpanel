<script setup lang="ts">
/**
 * What an empty chat shows: the plugin's introduction (the block's `intro`, else its placeholder as the
 * sentence) and its ready-made messages as chips. Pressing a chip asks the chat to send that message.
 */
import UiButton from '@/components/ui/UiButton.vue'
import UiChatAvatar from '@/components/ui/UiChatAvatar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PluginChatIntro, PluginChatSuggestion } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for PluginChatEmpty. */
withDefaults(defineProps<{
  /** The plugin's introduction; absent, the placeholder is the sentence. */
  intro?: PluginChatIntro
  /** The sentence shown when the introduction has none. */
  placeholder: string
  /** The ready-made messages. */
  suggestions: readonly PluginChatSuggestion[]
  /** Whether the chips are disabled, as while a reply streams. */
  disabled: boolean
}>(), {
  intro: undefined,
})

/** Events emitted by PluginChatEmpty. */
const emit = defineEmits<{
  /** The operator pressed a chip: the message to send. */
  pick: [text: string]
}>()
</script>

<template>
  <UiStack :gap="UiGap.Sm" :align="UiAlignItems.Center" class="py-6 text-center" data-testid="plugin-chat-empty">
    <UiChatAvatar />
    <UiText v-if="intro?.title" class="font-semibold" data-testid="plugin-chat-intro-title">{{ intro.title }}</UiText>
    <UiText :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-chat-intro-text">{{ intro?.text ?? placeholder }}</UiText>
    <UiStack v-if="suggestions.length > 0" :direction="UiDirection.Row" :gap="UiGap.Xs" :justify="UiJustify.Center" wrap data-testid="plugin-chat-suggestions">
      <UiButton
        v-for="(suggestion, position) in suggestions"
        :key="`${position}-${suggestion.label}`"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :disabled="disabled"
        :data-testid="`plugin-chat-suggestion-${position}`"
        @click="emit('pick', suggestion.text ?? suggestion.label)"
      >
        {{ suggestion.label }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>
