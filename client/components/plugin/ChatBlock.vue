<template>
  <UiCard class="flex flex-1 flex-col md:min-h-0" data-testid="plugin-chat">
    <UiStack gap="md" class="flex-1 md:min-h-0">
      <!-- An empty chat shows the plugin's introduction and its ready-made messages, which only fill the field. -->
      <UiStack v-if="messages.length === 0 && (block.intro || block.suggestions?.length)" gap="sm" data-testid="plugin-chat-intro">
        <UiText v-if="block.intro?.title" as="p" weight="semibold" data-testid="plugin-chat-intro-title">{{ block.intro.title }}</UiText>
        <UiText v-if="block.intro?.text" as="p" size="sm" tone="muted" data-testid="plugin-chat-intro-text">{{ block.intro.text }}</UiText>
        <UiStack v-if="block.suggestions?.length" direction="row" gap="sm" wrap>
          <UiButton
            v-for="(suggestion, position) in block.suggestions"
            :key="`${position}-${suggestion.label}`"
            variant="outline"
            size="sm"
            :data-testid="`plugin-chat-suggestion-${position}`"
            @click="draft = suggestion.text"
          >
            {{ suggestion.label }}
          </UiButton>
        </UiStack>
      </UiStack>
      <!-- The log scrolls by itself, so the composer below it stays at the bottom of the pane. -->
      <div
        ref="log"
        :class="['min-h-64 flex-1 overflow-y-auto', inPane ? 'max-h-[50dvh] md:max-h-none' : 'max-h-[60dvh]']"
        data-testid="plugin-chat-log"
        @scroll="onLogScroll"
      >
        <UiStack gap="sm" data-testid="plugin-chat-messages">
          <template v-for="message in messages" :key="message.key">
            <UiChatBubble
              v-if="hasBubble(message)"
              :side="message.role === PluginChatRole.User ? UiChatBubbleSide.Own : UiChatBubbleSide.Other"
              :data-testid="`plugin-chat-${message.role}`"
            >
              <UiText
                v-if="message.role === PluginChatRole.Assistant && block.assistant?.label"
                as="span"
                size="xs"
                tone="muted"
                class="block"
                data-testid="plugin-chat-assistant-label"
              >{{ block.assistant.label }}</UiText>
              <UiMarkdownText v-if="message.role === PluginChatRole.Assistant" :text="message.text" />
              <template v-else>{{ message.text }}</template>
              <UiStack v-if="message.selectionChips?.length" direction="row" gap="xs" wrap class="mt-1" data-testid="plugin-chat-message-selection">
                <UiBadge v-for="chip in message.selectionChips" :key="chip.id">{{ chip.label }}</UiBadge>
              </UiStack>
              <template v-for="link in message.links" :key="link.key">
                <UiLink v-if="link.external" :href="link.href" external class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
                <UiLink v-else :to="link.href" class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
              </template>
            </UiChatBubble>
            <UiCard
              v-if="message.steps.length"
              padding="none"
              class="px-2.5 py-2"
              data-testid="plugin-chat-steps-box"
            >
              <UiStack as="ul" gap="xs" data-testid="plugin-chat-steps">
                <UiStack
                  v-for="step in message.steps"
                  :key="step.key"
                  as="li"
                  direction="row"
                  gap="xs"
                  align="center"
                  :data-state="step.state"
                  data-testid="plugin-chat-step"
                >
                  <component :is="STEP_ICONS[step.state]" :size="14" :class="STEP_ICON_CLASSES[step.state]" aria-hidden="true" />
                  <UiText as="span" size="xs" tone="muted">{{ step.label }}</UiText>
                  <UiText v-if="step.state === PluginChatStepState.Stopped" as="span" size="xs" tone="muted" data-testid="plugin-chat-step-stopped">{{ $t('plugins.chatStepStopped') }}</UiText>
                </UiStack>
              </UiStack>
              <UiText as="p" size="xs" tone="muted" class="mt-1" data-testid="plugin-chat-steps-count">{{ $t('plugins.chatStepsCount', { done: doneSteps(message), total: message.steps.length }) }}</UiText>
            </UiCard>
            <UiAlert v-for="issue in message.issues" :key="issue.key" variant="error" data-testid="plugin-chat-issue">{{ issue.text }}</UiAlert>
            <UiToneCard v-for="result in message.results" :key="result.key" :tone="pluginUiTone(result.tone)" data-testid="plugin-chat-result">
              <UiStack gap="xs">
                <UiText v-if="result.title" as="p" weight="semibold" data-testid="plugin-chat-result-title">{{ result.title }}</UiText>
                <UiText v-if="result.text" as="p" size="sm" tone="muted" data-testid="plugin-chat-result-text">{{ result.text }}</UiText>
                <UiStack v-if="result.actions.length || result.details.length" direction="row" gap="sm" align="center" wrap>
                  <template v-for="action in result.actions" :key="action.key">
                    <PluginActionBlock v-if="action.block" :block="action.block" />
                    <UiButton v-else-if="action.resend" size="sm" :disabled="streaming" data-testid="plugin-chat-result-resend" @click="resend">{{ action.label }}</UiButton>
                    <UiLink v-else-if="action.link?.external" :href="action.link.href" external testid="plugin-chat-result-link">{{ action.label }}</UiLink>
                    <UiLink v-else-if="action.link" :to="action.link.href" testid="plugin-chat-result-link">{{ action.label }}</UiLink>
                  </template>
                  <UiButton
                    v-if="result.details.length"
                    variant="outline"
                    size="sm"
                    data-testid="plugin-chat-result-details-toggle"
                    @click="toggleDetails(result.key)"
                  >{{ $t('plugins.chatResultDetails') }}</UiButton>
                </UiStack>
                <UiStack v-if="openDetails.has(result.key)" as="ul" gap="xs" data-testid="plugin-chat-result-details">
                  <UiText v-for="line in result.details" :key="line" as="li" size="xs" tone="muted">{{ line }}</UiText>
                </UiStack>
              </UiStack>
            </UiToneCard>
          </template>
        </UiStack>
      </div>
      <UiText v-if="status" size="xs" tone="muted" data-testid="plugin-chat-status">{{ status }}</UiText>
      <UiText v-if="error" size="sm" tone="danger" data-testid="plugin-chat-error">{{ error }}</UiText>
      <UiStack v-if="selection?.chips.value.length" gap="xs" data-testid="plugin-chat-selection">
        <UiStack
          v-for="chip in selection.chips.value"
          :key="chip.id"
          direction="row"
          gap="xs"
          align="center"
          class="rounded-md bg-primary-50 px-2 py-1 dark:bg-primary-900/30"
          data-testid="plugin-chat-selection-chip"
        >
          <UiText as="span" size="xs" class="min-w-0 flex-1 truncate">{{ chip.label }}</UiText>
          <UiButton
            :variant="UiButtonVariant.Ghost"
            size="sm"
            :aria-label="`${$t('plugins.chatSelectionRemove')}: ${chip.label}`"
            data-testid="plugin-chat-selection-remove"
            @click="selection?.remove(chip.id)"
          >{{ $t('plugins.chatSelectionRemove') }}</UiButton>
        </UiStack>
      </UiStack>
      <UiText v-if="selection?.error.value" size="sm" tone="danger" data-testid="plugin-chat-selection-error">{{ selection.error.value }}</UiText>
      <UiStack direction="row" gap="sm" align="end" data-testid="plugin-chat-composer">
        <UiTextarea
          v-model="draft"
          class="min-w-0 flex-1"
          :label="$t('plugins.chatMessage')"
          :placeholder="block.placeholder ?? $t('plugins.chatMessage')"
          :rows="2"
          :max-length="block.maxLength"
          testid="plugin-chat-input"
        />
        <UiButton v-if="streaming" variant="outline" size="sm" data-testid="plugin-chat-stop" @click="stop">{{ $t('plugins.chatStop') }}</UiButton>
        <UiButton v-else size="sm" :disabled="!canSend" data-testid="plugin-chat-send" @click="send">{{ $t('plugins.chatSend') }}</UiButton>
      </UiStack>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PluginChatBlock - a chat with the plugin: earlier messages read from the `history` GET route, and each message
 * sent to the streaming `send` route (both `{ method, path }`), whose events are shown as they arrive: `delta` adds
 * text to the reply, `message` replaces it, `status` says what the plugin is doing, `error` shows the sentence (the
 * plugin's `text`, or the host's localised `message` when the plugin failed mid-stream), `link` (`{ text, url }`)
 * adds a link to the reply when the link-block rule admits the address, `done` ends the reply.
 * `step` (`{ id?, label, state }`) keeps a compact list inside the reply, a repeated key updating its row; `result`
 * adds a card with a tone edge and actions under the same link and route rules as other blocks; `issue` shows the
 * plugin's own failure in the danger tone while the stream goes on, `error` and `done` staying the host's. A malformed
 * event of these three is ignored whole. Everything is shown as text, never HTML; a reply's own text is read as the
 * small Markdown subset of `UiMarkdownText`, which builds known tags with `h()`, and the reader's own message stays as typed.
 *
 * The stream is read through the store, which opens it with `apiStream` (fetch through the panel's own server,
 * with the CSRF and language headers), never with `EventSource`. Stop, leaving the page and a lost connection end
 * it, and the plugin's call ends with it. After a Stop a step still running shows as stopped and no failure is shown.
 *
 * The log scrolls on its own and follows the reply, unless the reader scrolled up.
 *
 * A card's action reads again the blocks named in `refresh`, as a finished reply does. A card may also keep `details`
 * lines behind a switch, and an action with `resend: true` sends the conversation's last message again.
 *
 * An empty chat shows the block's `intro` and its `suggestions`, which only fill the message field; `assistant.label`
 * signs each reply, `maxLength` caps the field, and once a reply has ended the blocks named in `refresh` read again,
 * since a turn may have changed what they show. With `selection: true` the elements selected on the preview the reader
 * last selected on show as chips over the field, each labelled by the plugin, never by the frame, and go with the next
 * message; sending ends that preview's selection, and a "try again" sends the same one while that preview still holds the
 * release it was selected on, and no selection after that, since the ids may name other content in a new release. When another block refreshes this one by its `id` (a "new
 * conversation" action), the reply in flight is stopped, the messages are cleared and the history is read again.
 */
import type { Component } from 'vue'
import { Check, CircleSlash, LoaderCircle, X } from 'lucide-vue-next'
import { UiButtonVariant, UiChatBubbleSide } from '~/utils/ui/enums'
import { usePluginSelection } from '~/composables/usePluginSelection'
import { usePluginPane, usePluginView } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginChatBlock } from '~/types/pluginblock'
import { PluginChatEventType } from '~/types/pluginchateventtype'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import { PluginChatRole } from '~/types/pluginchatrole'
import type { PluginSelectionChip, PluginSelectionClaim } from '~/types/pluginpreviewselection'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import { PluginChatStepState } from '~/types/pluginchatstepstate'
import { parsePluginChatResult, parsePluginChatStep, pluginLinkTarget, pluginUiTone, readPluginField } from '~/utils/plugins'

/** Props of PluginChatBlock. */
interface Props {
  /** The block. */
  block: PluginChatBlock
}

/** The props. */
const props = defineProps<Props>()

/** The view's context: the plugin and the route values. */
const context = usePluginView()
/** The selections of the document's previews; null when this chat takes none. */
const selections = props.block.selection ? usePluginSelection() : null
/** The selection this chat shows and sends: the one of the preview last selected on. */
const selection = computed(() => selections?.current.value ?? null)
/** The plugin store, which reads the history and the stream. */
const store = usePluginsStore()

/** Whether the chat sits in a split pane, which gives it its height on a wide screen. */
const inPane = usePluginPane()

/** The icon of each step state. */
const STEP_ICONS: Readonly<Record<PluginChatStepState, Component>> = {
  [PluginChatStepState.Running]: LoaderCircle,
  [PluginChatStepState.Done]: Check,
  [PluginChatStepState.Failed]: X,
  [PluginChatStepState.Stopped]: CircleSlash,
}

/** The icon colour of each step state, light and dark. */
const STEP_ICON_CLASSES: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: 'shrink-0 animate-spin text-primary-500 motion-reduce:animate-none',
  [PluginChatStepState.Done]: 'shrink-0 text-green-600 dark:text-green-400',
  [PluginChatStepState.Failed]: 'shrink-0 text-red-600 dark:text-red-400',
  [PluginChatStepState.Stopped]: 'shrink-0 text-gray-400',
}

/** How close to the end, in pixels, still counts as reading the end of the log. */
const FOLLOW_THRESHOLD_PX = 40

/** The most steps, cards or issues one reply keeps, so a stream cannot grow the page without end. */
const MAX_REPLY_PARTS = 100

/** The scrolling log. */
const log = ref<HTMLElement | null>(null)
/** Whether the log follows the reply; false once the reader scrolled up. */
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
const nextKey = ref(0)
/** Aborts the stream in flight, if any. */
let controller: AbortController | null = null
/** Counts the conversations shown; a history read or a reply from an earlier one is dropped when it lands. */
let generation = 0

/** Whether a message can be sent now. */
const canSend = computed(() => !streaming.value && draft.value.trim() !== '')

/**
 * The text an event carries: the plugin's `text`, else the host's `message` (its own error frame).
 *
 * @param event - The event.
 * @returns The text, or empty.
 */
const textOf = (event: PluginStreamEvent): string => {
  const text = readPluginField(event.data, 'text')
  if (typeof text === 'string') {
    return text
  }
  const message = readPluginField(event.data, 'message')
  return typeof message === 'string' ? message : ''
}

/** The most messages the chat keeps on screen; older ones drop off the top, so a long history cannot grow the page. */
const MAX_MESSAGES = 200

/**
 * Adds a link to a message when the link-block rule admits its address: host-relative, or https on one of the view's
 * allowed origins. Anything else is dropped, so a reply can never link where a link block could not.
 *
 * @param message - The message the link belongs to.
 * @param text - The link text.
 * @param url - The address, as the plugin wrote it.
 * @returns Nothing.
 */
const addLink = (message: PluginChatMessage, text: unknown, url: unknown): void => {
  const link = pluginLinkTarget(url, context.externalOrigins)
  if (link && typeof text === 'string' && text !== '') {
    message.links.push({ key: nextKey.value++, text, href: link.href, external: link.external })
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
  messages.value.push({ key: nextKey.value++, role, text, links: [], steps: [], results: [], issues: [] })
  if (messages.value.length > MAX_MESSAGES) {
    messages.value.splice(0, messages.value.length - MAX_MESSAGES)
  }
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
  // A refresh started a new conversation while this read was out: its answer belongs to the old one.
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
      // A history entry may keep the links its reply carried, `[{ text, url }]`; they pass the same rule as live ones.
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
 * Adds a step or updates the row its key names.
 *
 * @param target - The reply being written.
 * @param data - The event's data.
 * @returns Nothing.
 */
const applyStep = (target: PluginChatMessage, data: unknown): void => {
  const step = parsePluginChatStep(data)
  if (!step) {
    return
  }
  const row = target.steps.find(candidate => candidate.key === step.key)
  if (row) {
    row.label = step.label
    row.state = step.state
  } else if (target.steps.length < MAX_REPLY_PARTS) {
    target.steps.push(step)
  }
}

/**
 * Adds a result card.
 *
 * @param target - The reply being written.
 * @param data - The event's data.
 * @returns Nothing.
 */
const applyResult = (target: PluginChatMessage, data: unknown): void => {
  const result = parsePluginChatResult(data, context.values, context.externalOrigins, props.block.refresh ?? [])
  if (result && target.results.length < MAX_REPLY_PARTS) {
    target.results.push({ ...result, key: nextKey.value++ })
  }
}

/**
 * Adds the plugin's own failure.
 *
 * @param target - The reply being written.
 * @param data - The event's data.
 * @returns Nothing.
 */
const applyIssue = (target: PluginChatMessage, data: unknown): void => {
  const text = readPluginField(data, 'text')
  if (typeof text === 'string' && text !== '' && target.issues.length < MAX_REPLY_PARTS) {
    target.issues.push({ key: nextKey.value++, text })
  }
}

/**
 * Applies one event of the reply.
 *
 * @param target - The reply being written.
 * @param event - The event.
 * @returns Nothing.
 */
const apply = (target: PluginChatMessage, event: PluginStreamEvent): void => {
  switch (event.type) {
    case PluginChatEventType.Delta:
      target.text += textOf(event)
      break
    case PluginChatEventType.Message:
      target.text = textOf(event)
      break
    case PluginChatEventType.Status:
      status.value = textOf(event)
      break
    case PluginChatEventType.Error:
      error.value = textOf(event)
      break
    case PluginChatEventType.Link:
      addLink(target, textOf(event), readPluginField(event.data, 'url'))
      break
    case PluginChatEventType.Done:
      status.value = ''
      break
    case PluginChatEventType.Step:
      applyStep(target, event.data)
      break
    case PluginChatEventType.Result:
      applyResult(target, event.data)
      break
    case PluginChatEventType.Issue:
      applyIssue(target, event.data)
      break
    default:
      // An event this host does not know is ignored, so a plugin written for a newer host still chats here.
      break
  }
}

/**
 * Sends a message and shows the reply as it streams.
 *
 * @param text - The message, already trimmed.
 * @param claim - The selected elements to send with it, if any.
 * @param chips - Those elements as the plugin named them, shown on the message.
 * @param owner - The preview they were selected on.
 * @returns Resolves when the reply has ended, been stopped or failed.
 */
const sendMessage = async (text: string, claim?: PluginSelectionClaim, chips?: readonly PluginSelectionChip[], owner?: string): Promise<void> => {
  const sent = add(PluginChatRole.User, text)
  if (claim) {
    sent.selection = claim
    sent.selectionChips = chips ?? []
    sent.selectionOwner = owner ?? ''
  }
  const target = add(PluginChatRole.Assistant, '')
  const asked = generation
  const running = new AbortController()
  controller = running
  following = true
  streaming.value = true
  status.value = ''
  error.value = ''

  const result = await store.streamRoute(
    context.pluginId, props.block.send, context.values, claim ? { message: text, selection: claim } : { message: text }, event => apply(target, event), running.signal)

  // The conversation was reset while the reply streamed: the reset already stopped it and cleared the screen.
  if (asked !== generation) {
    return
  }
  streaming.value = false
  status.value = ''
  controller = null
  // The reader stopped watching: what is still running is not known to have finished, and is not called a failure.
  if (running.signal.aborted) {
    for (const step of target.steps) {
      if (step.state === PluginChatStepState.Running) {
        step.state = PluginChatStepState.Stopped
      }
    }
  }
  // A refusal or a broken connection shows the store's sentence; an unusable route reference (`unavailable`) has
  // no sentence of its own to show, and a stop or a finished stream is `ok`.
  if (!result.ok && !result.unavailable) {
    error.value = result.error
  }
  // A turn that ran may have changed what other blocks show (a new version, fewer credits), stopped or not.
  if (!result.unavailable && props.block.refresh?.length) {
    context.refresh(props.block.refresh)
  }
  // A reply that never wrote a word (a refusal before the first event) leaves no empty bubble behind.
  if (target.text === '' && target.links.length === 0 && target.steps.length === 0 && target.results.length === 0 && target.issues.length === 0) {
    messages.value = messages.value.filter(message => message.key !== target.key)
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
  const source = selection.value
  const claim = source?.claim() ?? undefined
  const chips = source ? [...source.chips.value] : undefined
  source?.clear()
  await sendMessage(text, claim, chips, source?.owner)
}

/**
 * Sends the conversation's last message again, for a card's "try again". It never passes through the field, so what the
 * reader has started typing there stays.
 *
 * @returns Resolves when the reply has ended.
 */
const resend = async (): Promise<void> => {
  const last = [...messages.value].reverse().find(message => message.role === PluginChatRole.User)
  if (last && !streaming.value) {
    // A release moved on since: its ids may name other content now, so the old claim is not sent again.
    const current = last.selection && selections?.releaseOf(last.selectionOwner ?? '') === last.selection.releaseId
    await sendMessage(last.text, current ? last.selection : undefined, current ? last.selectionChips : undefined, last.selectionOwner)
  }
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
  message.steps.filter(step => step.state === PluginChatStepState.Done).length

/**
 * Stops the reply streaming now; what arrived stays.
 *
 * @returns Nothing.
 */
const stop = (): void => {
  controller?.abort()
}

/**
 * Starts a new conversation on screen: stops the reply in flight, clears what is shown and reads the history again,
 * which the plugin answers for the conversation it now holds.
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
 * Notes whether the reader is at the end of the log.
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

// After the render, when the new height is known; a reader who scrolled up keeps their place.
watch(messages, () => {
  if (following && log.value) {
    log.value.scrollTop = log.value.scrollHeight
  }
}, { deep: true, flush: 'post' })

// Another block (a reset action) names this block in its `refresh`: the conversation it showed is gone.
watch(() => (props.block.id ? context.refreshes.value[props.block.id] ?? 0 : 0), restart)

// Leaving the page closes the stream, so the plugin stops writing for nobody.
onBeforeUnmount(stop)
</script>
