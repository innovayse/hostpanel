<template>
  <UiStack v-if="message.role === PluginChatRole.User" :gap="UiGap.None" :align="UiCrossAlign.End">
    <UiChatBubble side="own" :data-testid="`plugin-chat-${message.role}`">
      {{ message.text }}
      <UiStack v-if="message.selectionChips?.length" :direction="UiDirection.Row" :gap="UiGap.Xs" wrap class="mt-1" data-testid="plugin-chat-message-selection">
        <UiBadge v-for="chip in message.selectionChips" :key="chip.id">{{ chip.label }}</UiBadge>
      </UiStack>
    </UiChatBubble>
    <time v-if="message.sentAt" :datetime="isoTime" :class="TIME_CLASSES" data-testid="plugin-chat-time">{{ time }}</time>
  </UiStack>
  <UiStack v-else :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiCrossAlign.Start" data-testid="plugin-chat-reply">
    <UiChatAvatar :label="assistantLabel" />
    <UiStack :gap="UiGap.Xs" class="min-w-0 max-w-[75ch] flex-1">
      <UiText v-if="assistantLabel" as="span" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" :weight="UiTextWeight.Medium" data-testid="plugin-chat-assistant-label">{{ assistantLabel }}</UiText>
      <UiStack v-if="message.text !== '' || message.links.length" :gap="UiGap.Xs" class="text-sm text-gray-800 dark:text-gray-200" :data-testid="`plugin-chat-${message.role}`">
        <UiMarkdownText
          v-if="message.text !== ''"
          :text="message.text"
          :copy-label="$t('plugins.chatCopyCode')"
          :copied-label="$t('plugins.chatCopied')"
          :caret="writing"
        />
        <template v-for="link in message.links" :key="link.key">
          <UiLink v-if="link.external" :href="link.href" external class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
          <UiLink v-else :to="link.href" class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
        </template>
      </UiStack>
      <UiTypingDots v-if="waiting" :label="$t('plugins.chatWriting')" />
      <UiStack v-if="writing && message.status" :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiCrossAlign.Center" role="status">
        <LoaderCircle :size="14" class="shrink-0 animate-spin text-primary-500 motion-reduce:animate-none" aria-hidden="true" />
        <UiText as="span" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-status">{{ message.status }}</UiText>
      </UiStack>
      <PluginChatSteps v-if="message.steps.length" :steps="message.steps" />
      <UiAlert v-for="issue in message.issues" :key="issue.key" :variant="UiAlertVariant.Error" data-testid="plugin-chat-issue">{{ issue.text }}</UiAlert>
      <PluginChatResultCard
        v-for="result in message.results"
        :key="result.key"
        :result="result"
        :refresh="refresh"
        :disabled="disabled"
        @resend="emit('resend')"
      />
      <UiStack v-if="!writing && (message.text !== '' || message.sentAt)" :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiCrossAlign.Center">
        <UiCopyButton
          v-if="message.text !== ''"
          :value="message.text"
          :label="$t('plugins.chatCopy')"
          :copied-label="$t('plugins.chatCopied')"
          testid="plugin-chat-copy"
        />
        <time v-if="message.sentAt" :datetime="isoTime" :class="TIME_CLASSES" data-testid="plugin-chat-time">{{ time }}</time>
      </UiStack>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginChatMessage: one message of a plugin chat. The reader's own message is a bubble on the right; the plugin's reply
 * is plain text beside an avatar and its optional name, up to about 75 characters wide, with a copy button.
 *
 * A reply is read as the small Markdown subset of `UiMarkdownText` (code blocks carry their own copy button) and ends in a
 * blinking caret while it is written. Before its first words it shows the typing dots; what the plugin says it is doing
 * (`status`) shows inside it with a spinner. Steps, issues and result cards follow the text. The time shows for
 * a message this session wrote, never for one read from the history.
 */
import { LoaderCircle } from 'lucide-vue-next'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import { PluginChatRole } from '~/types/pluginchatrole'
import { formatTime } from '~/utils/formatDate'
import { UiAlertVariant, UiCrossAlign, UiDirection, UiGap, UiTextSize, UiTextTone, UiTextWeight } from '~/utils/ui/enums'

/** Props of PluginChatMessage. */
interface Props {
  /** The message. */
  message: PluginChatMessage
  /** The name that signs a reply; absent, a reply is signed by the avatar alone. */
  assistantLabel?: string
  /** The ids of the blocks the chat reads again after a turn; passed on to a card's actions. */
  refresh: readonly string[]
  /** Whether a "try again" on a card is disabled, as while a reply streams. */
  disabled: boolean
  /** Whether this is the reply being written now. */
  writing: boolean
}

/** The props. */
const props = withDefaults(defineProps<Props>(), {
  assistantLabel: undefined,
})

/** The events of PluginChatMessage. */
const emit = defineEmits<{
  /** A card's action asked to send the conversation's last message again. */
  resend: []
}>()

/** The classes of the time: always shown, so a keyboard or touch reader sees it as well as a pointer one. */
const TIME_CLASSES = 'text-xs text-gray-500 dark:text-gray-400'

/** The time the message was written, for the reader. */
const time = computed(() => formatTime(props.message.sentAt))

/** The same moment for the `datetime` attribute. */
const isoTime = computed(() => (props.message.sentAt ? new Date(props.message.sentAt).toISOString() : undefined))

/** Whether the reply has shown nothing yet while it is written: the typing dots stand in. */
const waiting = computed(() =>
  props.writing && props.message.text === '' && props.message.status === '' && props.message.links.length === 0
  && props.message.steps.length === 0 && props.message.results.length === 0 && props.message.issues.length === 0)
</script>
