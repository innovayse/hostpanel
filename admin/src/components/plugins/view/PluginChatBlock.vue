<script setup lang="ts">
/**
 * A chat with the plugin: earlier messages read from the `history` GET route, and each message sent to
 * the streaming `send` route, whose events are shown as they arrive: `delta` adds text to the reply,
 * `message` replaces it, `status` says what the plugin is doing, `error` shows the sentence, `link`
 * (`{ text, url }`) adds a link when the link rule admits the address, `done` ends the reply. `step`
 * keeps a compact list inside the reply, a repeated key updating its row; `result` adds a card with
 * actions under the same link and route rules as other blocks; `issue` shows the plugin's own failure while
 * the stream goes on. A malformed event is ignored whole. Everything is text: a reply is read as Markdown
 * through `UiMarkdown` with its links kept as text, because an address renders only through the link rule.
 *
 * The stream is read through the store, which opens it with `fetch` (never `EventSource`) with the CSRF
 * and language headers. Stop and leaving the page end it, and the plugin's call ends with it; after a Stop
 * a step still running shows as stopped and no failure is shown. The log scrolls on its own and follows
 * the reply unless the operator scrolled up. An empty chat shows the block's `intro` and `suggestions`;
 * once a reply ended the blocks named in `refresh` read again. A refresh of this block's own `id` (a "new
 * conversation" action) stops the reply, clears the messages and reads the history again.
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginChatCardActions from '@/components/plugins/view/PluginChatCardActions.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChatBubble, { UiChatBubbleSide } from '@/components/ui/UiChatBubble.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiMarkdown from '@/components/ui/UiMarkdown.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { usePluginPane, usePluginView } from '@/composables/usePluginView'
import { Icon } from '@/lib/icons'
import { parsePluginChatResult, parsePluginChatStep, pluginAlertTone, pluginLinkTarget, readPluginField } from '@/lib/pluginView'
import { usePluginUiStore } from '@/stores/pluginUi'
import type { ServerSentEvent } from '@/types/http'
import { PluginChatEventType, PluginChatRole, PluginChatStepState } from '@/types/pluginview'
import type { PluginChatBlock, PluginChatMessage } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Props for PluginChatBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginChatBlock
}>()

/** Translation function. */
const { t } = useI18n()
/** The view's context: the plugin and the route values. */
const context = usePluginView()
/** The plugin ui store, which reads the history and the stream. */
const store = usePluginUiStore()
/** Whether the chat sits in a split pane, which bounds its log lower. */
const inPane = usePluginPane()

/** The icon of each step state. */
const STEP_ICONS: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: Icon.Refresh,
  [PluginChatStepState.Done]: Icon.Check,
  [PluginChatStepState.Failed]: Icon.Close,
  [PluginChatStepState.Stopped]: Icon.Remove,
}

/** The icon colour of each step state. */
const STEP_ICON_CLASSES: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: 'shrink-0 animate-spin text-primary-500 motion-reduce:animate-none',
  [PluginChatStepState.Done]: 'shrink-0 text-status-green',
  [PluginChatStepState.Failed]: 'shrink-0 text-status-red',
  [PluginChatStepState.Stopped]: 'shrink-0 text-text-muted',
}

/** How close to the end, in pixels, still counts as reading the end of the log. */
const FOLLOW_THRESHOLD_PX = 40

/** The most steps, cards or issues one reply keeps, so a stream cannot grow the page without end. */
const MAX_REPLY_PARTS = 100

/** The most messages the chat keeps on screen; older ones drop off the top. */
const MAX_MESSAGES = 200

/** The scrolling log. */
const log = ref<HTMLElement | null>(null)
/** Whether the log follows the reply; false once the operator scrolled up. */
let following = true

/** The messages shown, oldest first. */
const messages = ref<PluginChatMessage[]>([])
/** The message being written. */
const draft = ref('')
/** What the plugin says it is doing; empty when nothing. */
const status = ref('')
/** The sentence of a failure; empty when there is none. */
const error = ref('')
/** The keys of the cards whose details are open. */
const openDetails = ref<ReadonlySet<number>>(new Set())
/** True while a reply streams. */
const streaming = ref(false)
/** The next message key. */
let nextKey = 0
/** Aborts the stream in flight, if any. */
let controller: AbortController | null = null
/** Counts the conversations shown; a history read or a reply from an earlier one is dropped when it lands. */
let generation = 0

/** Whether a message can be sent now. */
const canSend = computed<boolean>(() => !streaming.value && draft.value.trim() !== '')

/** The height bound of the log: lower inside a split pane. */
const logClasses = computed<string>(() => (inPane ? 'max-h-[50dvh]' : 'max-h-[60dvh]'))

/**
 * The text an event carries: the plugin's `text`, else the host's `message` (its own error frame).
 *
 * @param event - The event.
 * @returns The text, or empty.
 */
const textOf = (event: ServerSentEvent): string => {
  const text = readPluginField(event.data, 'text')
  if (typeof text === 'string') {
    return text
  }
  const message = readPluginField(event.data, 'message')
  return typeof message === 'string' ? message : ''
}

/**
 * Adds a link to a message when the link rule admits its address.
 *
 * @param message - The message the link belongs to.
 * @param text - The link text.
 * @param url - The address, as the plugin wrote it.
 * @returns Nothing.
 */
const addLink = (message: PluginChatMessage, text: unknown, url: unknown): void => {
  const link = pluginLinkTarget(url, context.externalOrigins)
  if (link && typeof text === 'string' && text !== '') {
    message.links.push({ key: nextKey++, text, href: link.href, external: link.external })
  }
}

/**
 * Adds a message.
 *
 * @param role - Who wrote it.
 * @param text - The text.
 * @returns The message as the reactive list holds it, so later events extend it in place.
 */
const add = (role: PluginChatRole, text: string): PluginChatMessage => {
  messages.value.push({ key: nextKey++, role, text, links: [], steps: [], results: [], issues: [] })
  if (messages.value.length > MAX_MESSAGES) {
    messages.value.splice(0, messages.value.length - MAX_MESSAGES)
  }
  // The list was just pushed to, so its last entry exists.
  return messages.value[messages.value.length - 1] as PluginChatMessage
}

/**
 * Reads the earlier messages, when the view names a history route.
 *
 * @returns Resolves when the read has ended.
 */
const loadHistory = async (): Promise<void> => {
  if (!props.block.history) {
    return
  }
  const asked = generation
  const result = await store.callRoute(context.pluginId, props.block.history, context.values)
  if (asked !== generation) {
    return
  }
  if (!result.ok) {
    error.value = result.error
    return
  }
  for (const entry of Array.isArray(result.data) ? result.data : []) {
    const text = readPluginField(entry, 'text')
    if (typeof text === 'string') {
      const message = add(readPluginField(entry, 'role') === PluginChatRole.User ? PluginChatRole.User : PluginChatRole.Assistant, text)
      const links = readPluginField(entry, 'links')
      for (const link of Array.isArray(links) ? links : []) {
        addLink(message, readPluginField(link, 'text'), readPluginField(link, 'url'))
      }
    }
  }
}

/**
 * Whether a message draws its bubble: a reply made only of steps or cards draws none.
 *
 * @param message - The message.
 * @returns False for a reply with parts and no text or link.
 */
const hasBubble = (message: PluginChatMessage): boolean =>
  message.text !== '' || message.links.length > 0
  || (message.steps.length === 0 && message.results.length === 0 && message.issues.length === 0)

/**
 * Applies one event of the reply.
 *
 * @param target - The reply being written.
 * @param event - The event.
 * @returns Nothing.
 */
const apply = (target: PluginChatMessage, event: ServerSentEvent): void => {
  switch (event.type) {
    case PluginChatEventType.Delta: {
      target.text += textOf(event)
      break
    }
    case PluginChatEventType.Message: {
      target.text = textOf(event)
      break
    }
    case PluginChatEventType.Status: {
      status.value = textOf(event)
      break
    }
    case PluginChatEventType.Error: {
      error.value = textOf(event)
      break
    }
    case PluginChatEventType.Link: {
      addLink(target, textOf(event), readPluginField(event.data, 'url'))
      break
    }
    case PluginChatEventType.Done: {
      status.value = ''
      break
    }
    case PluginChatEventType.Step: {
      const step = parsePluginChatStep(event.data)
      const row = step ? target.steps.find((candidate) => candidate.key === step.key) : undefined
      if (step && row) {
        row.label = step.label
        row.state = step.state
      } else if (step && target.steps.length < MAX_REPLY_PARTS) {
        target.steps.push(step)
      }
      break
    }
    case PluginChatEventType.Result: {
      const result = parsePluginChatResult(event.data, context.values, context.externalOrigins, props.block.refresh ?? [])
      if (result && target.results.length < MAX_REPLY_PARTS) {
        target.results.push({ ...result, key: nextKey++ })
      }
      break
    }
    case PluginChatEventType.Issue: {
      const text = readPluginField(event.data, 'text')
      if (typeof text === 'string' && text !== '' && target.issues.length < MAX_REPLY_PARTS) {
        target.issues.push({ key: nextKey++, text })
      }
      break
    }
    default: {
      // An event this panel does not know is ignored, so a plugin written for a newer host still chats here.
      break
    }
  }
}

/**
 * Sends a message and shows the reply as it streams.
 *
 * @param text - The message, already trimmed.
 * @returns Resolves when the reply has ended, been stopped or failed.
 */
const sendMessage = async (text: string): Promise<void> => {
  add(PluginChatRole.User, text)
  const target = add(PluginChatRole.Assistant, '')
  const asked = generation
  const running = new AbortController()
  controller = running
  following = true
  streaming.value = true
  status.value = ''
  error.value = ''

  const result = await store.streamRoute(context.pluginId, props.block.send, context.values, { message: text }, (event) => apply(target, event), running.signal)

  // The conversation was reset while the reply streamed: the reset already stopped it and cleared the screen.
  if (asked !== generation) {
    return
  }
  streaming.value = false
  status.value = ''
  controller = null
  if (running.signal.aborted) {
    for (const step of target.steps) {
      if (step.state === PluginChatStepState.Running) {
        step.state = PluginChatStepState.Stopped
      }
    }
  }
  if (!result.ok && !result.unavailable) {
    error.value = result.error
  }
  // A turn that ran may have changed what other blocks show, stopped or not.
  if (!result.unavailable && props.block.refresh?.length) {
    context.refresh(props.block.refresh)
  }
  // A reply that never wrote a word leaves no empty bubble behind.
  if (target.text === '' && target.links.length === 0 && target.steps.length === 0 && target.results.length === 0 && target.issues.length === 0) {
    messages.value = messages.value.filter((message) => message.key !== target.key)
  }
}

/**
 * Sends the draft and empties the field.
 *
 * @returns Resolves when the reply has ended, been stopped or failed.
 */
const send = async (): Promise<void> => {
  const text = draft.value.trim()
  if (!text || streaming.value) {
    return
  }
  draft.value = ''
  await sendMessage(text)
}

/**
 * Sends the conversation's last message again, for a card's "try again"; the field is left as it is.
 *
 * @returns Resolves when the reply has ended.
 */
const resend = async (): Promise<void> => {
  const last = [...messages.value].reverse().find((message) => message.role === PluginChatRole.User)
  if (!last || streaming.value) {
    return
  }
  await sendMessage(last.text)
}

/**
 * Opens or closes a card's details.
 *
 * @param key - The card's key.
 * @returns Nothing.
 */
const toggleDetails = (key: number): void => {
  const next = new Set(openDetails.value)
  if (!next.delete(key)) {
    next.add(key)
  }
  openDetails.value = next
}

/**
 * How many of a reply's steps have finished.
 *
 * @param message - The reply.
 * @returns The count.
 */
const doneSteps = (message: PluginChatMessage): number =>
  message.steps.filter((step) => step.state === PluginChatStepState.Done).length

/**
 * Stops the reply streaming now; what arrived stays.
 *
 * @returns Nothing.
 */
const stop = (): void => {
  controller?.abort()
}

/**
 * Starts a new conversation on screen: stops the reply in flight, clears what is shown and reads the
 * history again.
 *
 * @returns Resolves when the history has been read again.
 */
const restart = async (): Promise<void> => {
  generation++
  stop()
  controller = null
  streaming.value = false
  messages.value = []
  status.value = ''
  error.value = ''
  await loadHistory()
}

/**
 * Notes whether the operator is at the end of the log.
 *
 * @returns Nothing.
 */
const onLogScroll = (): void => {
  const element = log.value
  if (element) {
    following = element.scrollHeight - element.scrollTop - element.clientHeight <= FOLLOW_THRESHOLD_PX
  }
}

onMounted(loadHistory)

// Leaving the page closes the stream, so the plugin stops writing for nobody.
onBeforeUnmount(stop)

// After the render, when the new height is known; an operator who scrolled up keeps their place.
watch(messages, () => {
  if (following && log.value) {
    log.value.scrollTop = log.value.scrollHeight
  }
}, { deep: true, flush: 'post' })

// Another block (a reset action) names this block in its `refresh`: the conversation it showed is gone.
watch(() => (props.block.id ? context.refreshes.value[props.block.id] ?? 0 : 0), restart)
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-chat">
    <UiStack :gap="UiGap.Md">
      <UiStack v-if="messages.length === 0 && (block.intro || block.suggestions?.length)" :gap="UiGap.Sm" data-testid="plugin-chat-intro">
        <UiText v-if="block.intro?.title" class="font-semibold" data-testid="plugin-chat-intro-title">{{ block.intro.title }}</UiText>
        <UiText v-if="block.intro?.text" :size="UiTextSize.Sm" :tone="UiTextTone.Muted" data-testid="plugin-chat-intro-text">{{ block.intro.text }}</UiText>
        <UiStack v-if="block.suggestions?.length" direction="row" :gap="UiGap.Sm" wrap>
          <UiButton
            v-for="(suggestion, position) in block.suggestions"
            :key="`${position}-${suggestion.label}`"
            :variant="UiButtonVariant.Secondary"
            :size="UiSize.Sm"
            :data-testid="`plugin-chat-suggestion-${position}`"
            @click="draft = suggestion.text"
          >
            {{ suggestion.label }}
          </UiButton>
        </UiStack>
      </UiStack>
      <!-- The log scrolls by itself, so the composer below it stays in view. -->
      <div ref="log" class="min-h-48 overflow-y-auto" :class="logClasses" data-testid="plugin-chat-log" @scroll="onLogScroll">
        <UiStack :gap="UiGap.Sm" data-testid="plugin-chat-messages">
          <template v-for="message in messages" :key="message.key">
            <UiChatBubble
              v-if="hasBubble(message)"
              :side="message.role === PluginChatRole.User ? UiChatBubbleSide.Own : UiChatBubbleSide.Other"
              :data-testid="`plugin-chat-${message.role}`"
            >
              <UiStack :gap="UiGap.Xs">
                <UiText
                  v-if="message.role === PluginChatRole.Assistant && block.assistant?.label"
                  :size="UiTextSize.Xs"
                  :tone="UiTextTone.Muted"
                  data-testid="plugin-chat-assistant-label"
                >{{ block.assistant.label }}</UiText>
                <UiMarkdown v-if="message.role === PluginChatRole.Assistant" :source="message.text" :links="false" />
                <template v-else>{{ message.text }}</template>
                <template v-for="link in message.links" :key="link.key">
                  <UiLink v-if="link.external" :href="link.href" external data-testid="plugin-chat-link">{{ link.text }}</UiLink>
                  <UiLink v-else :to="link.href" data-testid="plugin-chat-link">{{ link.text }}</UiLink>
                </template>
              </UiStack>
            </UiChatBubble>
            <UiCard v-if="message.steps.length > 0" :padding="UiCardPadding.Sm" data-testid="plugin-chat-steps-box">
              <UiStack :gap="UiGap.Xs" data-testid="plugin-chat-steps">
                <UiStack
                  v-for="step in message.steps"
                  :key="step.key"
                  direction="row"
                  :gap="UiGap.Xs"
                  :align="UiAlignItems.Center"
                  :data-state="step.state"
                  data-testid="plugin-chat-step"
                >
                  <UiIcon :markup="STEP_ICONS[step.state]" :size="UiSize.Sm" :class="STEP_ICON_CLASSES[step.state]" />
                  <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ step.label }}</UiText>
                  <UiText v-if="step.state === PluginChatStepState.Stopped" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-step-stopped">{{ t('plugins.pluginPage.chatStepStopped') }}</UiText>
                </UiStack>
                <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-steps-count">{{ t('plugins.pluginPage.chatStepsCount', { done: doneSteps(message), total: message.steps.length }) }}</UiText>
              </UiStack>
            </UiCard>
            <UiAlert v-for="issue in message.issues" :key="issue.key" :variant="UiTone.Danger" data-testid="plugin-chat-issue">{{ issue.text }}</UiAlert>
            <UiAlert v-for="result in message.results" :key="result.key" :variant="pluginAlertTone(result.tone)" data-testid="plugin-chat-result">
              <UiStack :gap="UiGap.Xs">
                <UiText v-if="result.title" class="font-semibold" data-testid="plugin-chat-result-title">{{ result.title }}</UiText>
                <UiText v-if="result.text" :size="UiTextSize.Sm" data-testid="plugin-chat-result-text">{{ result.text }}</UiText>
                <PluginChatCardActions
                  v-if="result.actions.length > 0 || result.details.length > 0"
                  :result="result"
                  :refresh="block.refresh ?? []"
                  :disabled="streaming"
                  @resend="resend"
                  @toggle-details="toggleDetails(result.key)"
                />
                <UiStack v-if="openDetails.has(result.key)" :gap="UiGap.Xs" data-testid="plugin-chat-result-details">
                  <UiText v-for="line in result.details" :key="line" :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ line }}</UiText>
                </UiStack>
              </UiStack>
            </UiAlert>
          </template>
        </UiStack>
      </div>
      <UiText v-if="status" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-status">{{ status }}</UiText>
      <UiText v-if="error" :size="UiTextSize.Sm" :tone="UiTextTone.Danger" data-testid="plugin-chat-error">{{ error }}</UiText>
      <UiStack direction="row" :gap="UiGap.Sm" :align="UiAlignItems.End" data-testid="plugin-chat-composer">
        <UiTextarea
          v-model="draft"
          class="min-w-0 flex-1"
          rows="2"
          :maxlength="block.maxLength"
          :aria-label="t('plugins.pluginPage.chatMessage')"
          :placeholder="block.placeholder ?? t('plugins.pluginPage.chatMessage')"
          data-testid="plugin-chat-input"
        />
        <UiButton v-if="streaming" :variant="UiButtonVariant.Secondary" :size="UiSize.Sm" data-testid="plugin-chat-stop" @click="stop">{{ t('plugins.pluginPage.chatStop') }}</UiButton>
        <UiButton v-else :size="UiSize.Sm" :disabled="!canSend" data-testid="plugin-chat-send" @click="send">{{ t('plugins.pluginPage.chatSend') }}</UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
