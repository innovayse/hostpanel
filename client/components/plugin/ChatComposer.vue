<template>
  <UiStack :gap="UiGap.Xs" data-testid="plugin-chat-composer">
    <UiChatInput
      ref="input"
      v-model="draft"
      :label="$t('plugins.chatMessage')"
      :placeholder="placeholder"
      :max-length="maxLength"
      testid="plugin-chat-input"
      @submit="emit('send')"
    >
      <template #action>
        <UiIconButton
          :icon="streaming ? Square : ArrowUp"
          :label="streaming ? $t('plugins.chatStop') : $t('plugins.chatSend')"
          :variant="UiIconButtonVariant.Primary"
          :disabled="!streaming && !canSend"
          :testid="streaming ? 'plugin-chat-stop' : 'plugin-chat-send'"
          @click="handleAction"
        />
      </template>
    </UiChatInput>
    <UiText as="p" :size="UiTextSize.Xs" :tone="UiTextTone.Subtle" class="hidden sm:block" data-testid="plugin-chat-hint">{{ $t('plugins.chatHint') }}</UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginChatComposer. The message field of a plugin chat: a growing field with the send control inside it, which turns
 * into Stop while a reply streams, and a quiet hint about the keyboard. Send and Stop are one button that changes its
 * icon, label and action, so the keyboard focus stays on it when a reply starts.
 *
 * It owns no draft: the text is the chat's (v-model), kept by `usePluginChatDraft`. Enter in the field and the Send
 * button both ask the chat to `send`; the chat decides whether a send is allowed. `focus()` puts the cursor back in the
 * field, which the chat does after every send.
 */
import { ArrowUp, Square } from 'lucide-vue-next'
import { UiGap, UiIconButtonVariant, UiTextSize, UiTextTone } from '~/utils/ui/enums'

/** Props of PluginChatComposer. */
interface Props {
  /** Hint shown inside the empty field. */
  placeholder: string
  /** The most characters a message may have. */
  maxLength?: number
  /** Whether a reply streams, which turns Send into Stop. */
  streaming: boolean
  /** Whether a message can be sent now. */
  canSend: boolean
}

/** The props. */
const props = withDefaults(defineProps<Props>(), {
  maxLength: undefined,
})

/** The message being written. */
const draft = defineModel<string>({ required: true })

/** The events of PluginChatComposer. */
const emit = defineEmits<{
  /** The reader asked to send the message. */
  send: []
  /** The reader asked to stop the reply. */
  stop: []
}>()

/** The field. */
const input = ref<{ focus: () => void } | null>(null)

/**
 * Runs the action button's current job: stops the reply while one streams, otherwise asks to send.
 *
 * @returns Nothing.
 */
const handleAction = (): void => {
  if (props.streaming) {
    emit('stop')
  } else {
    emit('send')
  }
}

/**
 * Puts the keyboard focus in the field.
 *
 * @returns Nothing.
 */
const focus = (): void => {
  input.value?.focus()
}

/** The handle the parent reaches the field through. */
defineExpose({ focus })
</script>
