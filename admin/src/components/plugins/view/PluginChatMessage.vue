<script setup lang="ts">
/**
 * One message of a plugin chat. The operator's own message is a bubble on the right; the plugin's reply is
 * plain text beside an avatar and its optional name, up to about 75 characters wide, with a copy button.
 *
 * A reply is read as Markdown through `UiMarkdown` with its links kept as text (an address renders only through
 * the link rule), its code blocks carry their own copy button, and it ends in a blinking caret while it is
 * written. Before its first words it shows the typing dots; what the plugin says it is doing (`status`) shows
 * inside it with a spinner. Steps, issues and result cards follow the text. The time shows for a
 * message this session wrote, never for one read from the history.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginChatResultCard from '@/components/plugins/view/PluginChatResultCard.vue'
import PluginChatSteps from '@/components/plugins/view/PluginChatSteps.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiChatAvatar from '@/components/ui/UiChatAvatar.vue'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'
import UiCopyButton from '@/components/ui/UiCopyButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTypingDots from '@/components/ui/UiTypingDots.vue'
import { formatTime } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { PluginChatRole } from '@/types/pluginview'
import type { PluginChatMessage } from '@/types/pluginview'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Props for PluginChatMessage. */
const props = withDefaults(defineProps<{
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
}>(), {
  assistantLabel: undefined,
})

/** Events emitted by PluginChatMessage. */
const emit = defineEmits<{
  /** A card's action asked to send the conversation's last message again. */
  resend: []
}>()

/** Translation function and the operator's locale. */
const { t, locale } = useI18n()

/** The classes of the time: always shown, so a keyboard or touch reader sees it as well as a pointer one. */
const TIME_CLASSES = 'text-[0.72rem] text-text-muted'

/** The time the message was written, for the operator. */
const time = computed<string>(() => formatTime(props.message.sentAt, locale.value))

/** The same moment for the `datetime` attribute. */
const isoTime = computed<string | undefined>(() => (props.message.sentAt === undefined ? undefined : new Date(props.message.sentAt).toISOString()))

/** Whether the reply has shown nothing yet while it is written: the typing dots stand in. */
const waiting = computed<boolean>(() =>
  props.writing && props.message.text === '' && props.message.status === '' && props.message.links.length === 0
  && props.message.steps.length === 0 && props.message.results.length === 0 && props.message.issues.length === 0)
</script>

<template>
  <UiStack v-if="message.role === PluginChatRole.User" :gap="UiGap.None" :align="UiAlignItems.End">
    <UiChatBubble :side="UiChatBubbleSide.Own" :data-testid="`plugin-chat-${message.role}`">{{ message.text }}</UiChatBubble>
    <time v-if="message.sentAt !== undefined" :datetime="isoTime" :class="TIME_CLASSES" data-testid="plugin-chat-time">{{ time }}</time>
  </UiStack>
  <UiStack v-else :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiAlignItems.Start" data-testid="plugin-chat-reply">
    <UiChatAvatar :label="assistantLabel" />
    <UiStack :gap="UiGap.Xs" class="min-w-0 max-w-[75ch] flex-1">
      <UiText v-if="assistantLabel" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="font-medium" data-testid="plugin-chat-assistant-label">{{ assistantLabel }}</UiText>
      <UiStack v-if="message.text !== '' || message.links.length > 0" :gap="UiGap.Xs" :data-testid="`plugin-chat-${message.role}`">
        <UiMarkdown
          v-if="message.text !== ''"
          :source="message.text"
          :links="false"
          :copy-label="t('plugins.pluginPage.chatCopyCode')"
          :copied-label="t('plugins.pluginPage.chatCopied')"
          :caret="writing"
        />
        <template v-for="link in message.links" :key="link.key">
          <UiLink v-if="link.external" :href="link.href" external data-testid="plugin-chat-link">{{ link.text }}</UiLink>
          <UiLink v-else :to="link.href" data-testid="plugin-chat-link">{{ link.text }}</UiLink>
        </template>
      </UiStack>
      <UiTypingDots v-if="waiting" :label="t('plugins.pluginPage.chatWriting')" />
      <UiStack v-if="writing && message.status" :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiAlignItems.Center" role="status">
        <UiIcon :markup="Icon.Refresh" :size="UiSize.Sm" class="animate-spin text-primary-500 motion-reduce:animate-none" />
        <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-status">{{ message.status }}</UiText>
      </UiStack>
      <PluginChatSteps v-if="message.steps.length > 0" :steps="message.steps" />
      <UiAlert v-for="issue in message.issues" :key="issue.key" :variant="UiTone.Danger" data-testid="plugin-chat-issue">{{ issue.text }}</UiAlert>
      <PluginChatResultCard
        v-for="result in message.results"
        :key="result.key"
        :result="result"
        :refresh="refresh"
        :disabled="disabled"
        @resend="emit('resend')"
      />
      <UiStack v-if="!writing && (message.text !== '' || message.sentAt !== undefined)" :direction="UiDirection.Row" :gap="UiGap.Xs" :align="UiAlignItems.Center">
        <UiCopyButton
          v-if="message.text !== ''"
          :value="message.text"
          :label="t('plugins.pluginPage.chatCopy')"
          :copied-label="t('plugins.pluginPage.chatCopied')"
          testid="plugin-chat-copy"
        />
        <time v-if="message.sentAt !== undefined" :datetime="isoTime" :class="TIME_CLASSES" data-testid="plugin-chat-time">{{ time }}</time>
      </UiStack>
    </UiStack>
  </UiStack>
</template>
