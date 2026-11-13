<template>
  <UiStack :gap="UiGap.Sm" :align="UiCrossAlign.Center" class="py-6 text-center" data-testid="plugin-chat-empty">
    <UiChatAvatar />
    <UiText v-if="intro?.title" as="p" :weight="UiTextWeight.Semibold" data-testid="plugin-chat-intro-title">{{ intro.title }}</UiText>
    <UiText as="p" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-chat-intro-text">{{ intro?.text ?? placeholder }}</UiText>
    <UiStack v-if="suggestions.length" :direction="UiDirection.Row" :gap="UiGap.Xs" :justify="UiJustify.Center" wrap data-testid="plugin-chat-suggestions">
      <UiButton
        v-for="(suggestion, position) in suggestions"
        :key="`${position}-${suggestion.label}`"
        :variant="UiButtonVariant.Outline"
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

<script setup lang="ts">
/**
 * PluginChatEmpty. What an empty chat shows: the plugin's introduction (the block's `intro`, else its placeholder as
 * the sentence) and its ready-made messages as chips. Pressing a chip asks the chat to send that message.
 */
import type { PluginChatSuggestion } from '~/types/pluginblock'
import { UiButtonVariant, UiCrossAlign, UiDirection, UiGap, UiJustify, UiSize, UiTextSize, UiTextTone, UiTextWeight } from '~/utils/ui/enums'

/** Props of PluginChatEmpty. */
interface Props {
  /** The plugin's introduction; absent, the placeholder is the sentence. */
  intro?: {
    /** The heading. */
    title?: string
    /** The sentence below it. */
    text?: string
  }
  /** The sentence shown when the introduction has none. */
  placeholder: string
  /** The ready-made messages. */
  suggestions: readonly PluginChatSuggestion[]
  /** Whether the chips are disabled, as while a reply streams. */
  disabled: boolean
}

/** The props. */
withDefaults(defineProps<Props>(), {
  intro: undefined,
})

/** The events of PluginChatEmpty. */
const emit = defineEmits<{
  /** The reader pressed a chip: the message to send. */
  pick: [text: string]
}>()
</script>
