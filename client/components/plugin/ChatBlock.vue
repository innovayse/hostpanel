<template>
  <UiCard data-testid="plugin-chat">
    <UiStack gap="md">
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
      <UiStack gap="sm" data-testid="plugin-chat-messages">
        <UiChatBubble
          v-for="message in messages"
          :key="message.key"
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
          {{ message.text }}
          <template v-for="link in message.links" :key="link.key">
            <UiLink v-if="link.external" :href="link.href" external class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
            <UiLink v-else :to="link.href" class="block" testid="plugin-chat-link">{{ link.text }}</UiLink>
          </template>
        </UiChatBubble>
      </UiStack>
      <UiText v-if="status" size="xs" tone="muted" data-testid="plugin-chat-status">{{ status }}</UiText>
      <UiText v-if="error" size="sm" tone="danger" data-testid="plugin-chat-error">{{ error }}</UiText>
      <UiTextarea
        v-model="draft"
        :label="$t('plugins.chatMessage')"
        :placeholder="block.placeholder ?? $t('plugins.chatMessage')"
        :rows="3"
        :max-length="block.maxLength"
        testid="plugin-chat-input"
      />
      <UiStack direction="row" gap="sm" justify="end">
        <UiButton v-if="streaming" variant="outline" size="sm" data-testid="plugin-chat-stop" @click="stop">{{ $t('plugins.chatStop') }}</UiButton>
        <UiButton size="sm" :disabled="!canSend" :loading="streaming" data-testid="plugin-chat-send" @click="send">{{ $t('plugins.chatSend') }}</UiButton>
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
 * Everything is shown as text, never HTML.
 *
 * The stream is read through the store, which opens it with `apiStream` (fetch through the panel's own server,
 * with the CSRF and language headers), never with `EventSource`. Stop, leaving the page and a lost connection end
 * it, and the plugin's call ends with it.
 *
 * An empty chat shows the block's `intro` and its `suggestions`, which only fill the message field; `assistant.label`
 * signs each reply, `maxLength` caps the field, and once a reply has ended the blocks named in `refresh` read again,
 * since a turn may have changed what they show. When another block refreshes this one by its `id` (a "new
 * conversation" action), the reply in flight is stopped, the messages are cleared and the history is read again.
 */
import { UiChatBubbleSide } from '~/utils/ui/enums'
import { usePluginView } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginChatBlock } from '~/types/pluginblock'
import { PluginChatEventType } from '~/types/pluginchateventtype'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import { PluginChatRole } from '~/types/pluginchatrole'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import { pluginLinkTarget, readPluginField } from '~/utils/plugins'

/** Props of PluginChatBlock. */
interface Props {
  /** The block. */
  block: PluginChatBlock
}

/** The props. */
const props = defineProps<Props>()

/** The view's context: the plugin and the route values. */
const context = usePluginView()
/** The plugin store, which reads the history and the stream. */
const store = usePluginsStore()

/** The messages shown, oldest first. */
const messages = ref<PluginChatMessage[]>([])
/** The message being written. */
const draft = ref('')
/** What the plugin says it is doing; empty when nothing. */
const status = ref('')
/** The sentence of a failure; empty when there is none. */
const error = ref('')
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
  messages.value.push({ key: nextKey.value++, role, text, links: [] })
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
    default:
      // An event this host does not know is ignored, so a plugin written for a newer host still chats here.
      break
  }
}

/**
 * Sends the draft and shows the reply as it streams.
 *
 * @returns Resolves when the reply has ended, been stopped or failed.
 */
const send = async (): Promise<void> => {
  const text = draft.value.trim()
  if (!text || streaming.value) {
    return
  }
  add(PluginChatRole.User, text)
  draft.value = ''
  const target = add(PluginChatRole.Assistant, '')
  const asked = generation
  controller = new AbortController()
  streaming.value = true
  status.value = ''
  error.value = ''

  const result = await store.streamRoute(
    context.pluginId, props.block.send, context.values, { message: text }, event => apply(target, event), controller.signal)

  // The conversation was reset while the reply streamed: the reset already stopped it and cleared the screen.
  if (asked !== generation) {
    return
  }
  streaming.value = false
  status.value = ''
  controller = null
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
  if (target.text === '' && target.links.length === 0) {
    messages.value = messages.value.filter(message => message.key !== target.key)
  }
}

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

onMounted(loadHistory)

// Another block (a reset action) names this block in its `refresh`: the conversation it showed is gone.
watch(() => (props.block.id ? context.refreshes.value[props.block.id] ?? 0 : 0), restart)

// Leaving the page closes the stream, so the plugin stops writing for nobody.
onBeforeUnmount(stop)
</script>
