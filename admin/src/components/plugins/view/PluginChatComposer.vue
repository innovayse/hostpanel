<script setup lang="ts">
/**
 * The message field of a plugin chat: a growing field with the send control inside it, which turns into Stop
 * while a reply streams, and a quiet hint about the keyboard. Send and Stop are one button that changes its icon,
 * label and action, so the keyboard focus stays on it when a reply starts.
 *
 * It owns no draft: the text is the chat's (v-model), kept by `usePluginChatDraft`. Enter in the field and the
 * Send button both ask the chat to `send`; the chat decides whether a send is allowed. `focus()` puts the
 * cursor back in the field, which the chat does after every send.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiChatInput from '@/components/ui/UiChatInput.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for PluginChatComposer. */
const props = withDefaults(defineProps<{
  /** Hint shown inside the empty field. */
  placeholder: string
  /** The most characters a message may have. */
  maxLength?: number
  /** Whether a reply streams, which turns Send into Stop. */
  streaming: boolean
  /** Whether a message can be sent now. */
  canSend: boolean
}>(), {
  maxLength: undefined,
})

/** The message being written. */
const draft = defineModel<string>({ required: true })

/** Events emitted by PluginChatComposer. */
const emit = defineEmits<{
  /** The operator asked to send the message. */
  send: []
  /** The operator asked to stop the reply. */
  stop: []
}>()

/** Translation function. */
const { t } = useI18n()

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

<template>
  <UiStack :gap="UiGap.Xs" data-testid="plugin-chat-composer">
    <UiChatInput
      ref="input"
      v-model="draft"
      :label="t('plugins.pluginPage.chatMessage')"
      :placeholder="placeholder"
      :max-length="maxLength"
      testid="plugin-chat-input"
      @submit="emit('send')"
    >
      <template #action>
        <UiIconButton
          :label="streaming ? t('plugins.pluginPage.chatStop') : t('plugins.pluginPage.chatSend')"
          :variant="UiButtonVariant.Primary"
          :size="UiSize.Sm"
          :disabled="!streaming && !canSend"
          :data-testid="streaming ? 'plugin-chat-stop' : 'plugin-chat-send'"
          @click="handleAction"
        >
          <UiIcon :markup="streaming ? Icon.Stop : Icon.ArrowUp" :size="UiSize.Sm" />
        </UiIconButton>
      </template>
    </UiChatInput>
    <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="hidden sm:block" data-testid="plugin-chat-hint">{{ t('plugins.pluginPage.chatHint') }}</UiText>
  </UiStack>
</template>
