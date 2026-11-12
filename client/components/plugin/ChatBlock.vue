<template>
  <UiCard class="flex flex-1 flex-col md:min-h-0" data-testid="plugin-chat">
    <UiStack :gap="UiGap.Sm" class="flex-1 md:min-h-0">
      <!-- The log fills the pane and scrolls by itself, so the composer below it stays at the bottom. -->
      <UiStack :gap="UiGap.None" class="relative flex-1 md:min-h-0">
        <div
          ref="log"
          role="log"
          tabindex="0"
          aria-live="polite"
          :aria-busy="streaming"
          :aria-label="$t('plugins.chatLog')"
          :class="['min-h-64 flex-1 overflow-y-auto', inPane ? 'max-h-[50dvh] md:max-h-none' : 'max-h-[60dvh]']"
          data-testid="plugin-chat-log"
          @scroll="onScroll"
        >
          <PluginChatEmpty
            v-if="loaded && messages.length === 0"
            :intro="block.intro"
            :placeholder="block.placeholder ?? $t('plugins.chatEmpty')"
            :suggestions="block.suggestions ?? []"
            :disabled="streaming"
            @pick="pick"
          />
          <UiStack :gap="UiGap.Sm" data-testid="plugin-chat-messages">
            <PluginChatMessage
              v-for="message in messages"
              :key="message.key"
              :message="message"
              :assistant-label="block.assistant?.label"
              :refresh="block.refresh ?? []"
              :disabled="streaming"
              :writing="isWriting(message.key)"
              @resend="resendLast(false)"
            />
            <UiAlert v-if="error" :variant="UiAlertVariant.Error">
              <UiText as="span" :size="UiTextSize.Sm" data-testid="plugin-chat-error">{{ error }}</UiText>
              <template v-if="retryable" #action>
                <UiButton :variant="UiButtonVariant.Outline" :size="UiSize.Sm" :disabled="streaming" data-testid="plugin-chat-retry" @click="resendLast(true)">{{ $t('plugins.chatRetry') }}</UiButton>
              </template>
            </UiAlert>
          </UiStack>
        </div>
        <UiStack v-if="!following" :align="UiCrossAlign.Center" class="pointer-events-none absolute inset-x-0 bottom-2">
          <UiButton :variant="UiButtonVariant.Outline" :size="UiSize.Sm" class="pointer-events-auto bg-white shadow dark:bg-gray-900" data-testid="plugin-chat-jump" @click="jumpToLatest">
            <ArrowDown :size="14" class="mr-1" aria-hidden="true" />
            {{ $t('plugins.chatJumpToLatest') }}
          </UiButton>
        </UiStack>
      </UiStack>
      <UiStack v-if="selection?.chips.value.length" :gap="UiGap.Xs" data-testid="plugin-chat-selection">
        <UiStack
          v-for="chip in selection.chips.value"
          :key="chip.id"
          :direction="UiDirection.Row"
          :gap="UiGap.Xs"
          :align="UiCrossAlign.Center"
          class="rounded-md bg-primary-50 px-2 py-1 dark:bg-primary-900/30"
          data-testid="plugin-chat-selection-chip"
        >
          <UiText as="span" :size="UiTextSize.Xs" class="min-w-0 flex-1 truncate">{{ chip.label }}</UiText>
          <UiButton
            :variant="UiButtonVariant.Ghost"
            :size="UiSize.Sm"
            :aria-label="`${$t('plugins.chatSelectionRemove')}: ${chip.label}`"
            data-testid="plugin-chat-selection-remove"
            @click="selection?.remove(chip.id)"
          >{{ $t('plugins.chatSelectionRemove') }}</UiButton>
        </UiStack>
      </UiStack>
      <UiText v-if="selection?.error.value" :size="UiTextSize.Sm" :tone="UiTextTone.Danger" data-testid="plugin-chat-selection-error">{{ selection.error.value }}</UiText>
      <UiText v-if="selectionStale" :size="UiTextSize.Sm" :tone="UiTextTone.Danger" role="alert" data-testid="plugin-chat-selection-stale">{{ $t('plugins.chatSelectionStale') }}</UiText>
      <PluginChatComposer
        ref="composer"
        v-model="draft"
        :placeholder="block.placeholder ?? $t('plugins.chatMessage')"
        :max-length="block.maxLength"
        :streaming="streaming"
        :can-send="canSend"
        @send="send"
        @stop="stop"
      />
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
 * The events become messages in `usePluginChatMessages`; the message, the empty state, the composer and the step list are
 * their own components (PluginChatMessage, PluginChatEmpty, PluginChatComposer, PluginChatSteps).
 *
 * The stream is read through the store, which opens it with `apiStream` (fetch through the panel's own server,
 * with the CSRF and language headers), never with `EventSource`. Stop, leaving the page and a lost connection end
 * it, and the plugin's call ends with it. After a Stop a step still running shows as stopped and no failure is shown.
 *
 * The log (`role="log"`) scrolls on its own and follows the reply, unless the reader scrolled up, when "Jump to latest"
 * offers the way back (`usePluginChatScroll`). Enter sends, Shift+Enter writes a new line, and the field takes the focus
 * again after every send.
 *
 * The unsent draft is kept in the tab's session storage (`usePluginChatDraft`) and forgotten once a reply arrived, or on
 * sign-out; leaving the page, or a Stop before the first word, keeps it. A send that got no reply (it failed, was
 * stopped, or went to a route the page cannot call) gives its text back to the field and is not left shown as sent; a
 * failed turn with a reply, or one that carried a selection, keeps its messages and offers "Retry", which replaces the
 * failed turn with a send of the same message. The turns themselves are `usePluginChatTurn`, the same in both apps;
 * this block draws them and supplies the element selection.
 *
 * A card's action reads again the blocks named in `refresh`, as a finished reply does, whether the reply ended well or
 * not. A card may also keep `details` lines behind a switch, and an action with `resend: true` sends the conversation's
 * last message again. An action with `visibleWhen` shows only while its condition holds, read again whenever a block
 * named in `refresh` is refreshed (PluginChatCardAction), so a card stops offering what no longer applies; the row of
 * actions hides itself when every action is hidden (PluginChatCardActions).
 *
 * An empty chat shows the block's `intro` and its `suggestions`, which send their text when pressed; `assistant.label`
 * signs each reply, `maxLength` caps the field. With `selection: true` the elements selected on the preview the reader
 * last selected on show as chips over the field, each labelled by the plugin, never by the frame, and go with the next
 * message; sending ends that preview's selection, and a "try again" sends the same one while that preview still holds the
 * release it was selected on. After that it sends nothing: the ids may name other content in a new release, and the message
 * without them would reach further than what the reader chose, so the reader is asked to select again and the text waits in
 * the field. When another block refreshes this one by its `id` (a "new
 * conversation" action), the reply in flight is stopped, the messages are cleared and the history is read again.
 */
import { ArrowDown } from 'lucide-vue-next'
import { UiAlertVariant, UiButtonVariant, UiCrossAlign, UiDirection, UiGap, UiSize, UiTextSize, UiTextTone } from '~/utils/ui/enums'
import { usePluginChatTurn } from '~/composables/usePluginChatTurn'
import type { PluginChatCarried, PluginChatSelectionHooks } from '~/composables/usePluginChatTurn'
import { usePluginSelection } from '~/composables/usePluginSelection'
import { usePluginPane } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginChatBlock } from '~/types/pluginblock'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import type { PluginSelectionChip, PluginSelectionClaim } from '~/types/pluginpreviewselection'

/** Props of PluginChatBlock. */
interface Props {
  /** The block. */
  block: PluginChatBlock
}

/** The props. */
const props = defineProps<Props>()

/** The selections of the document's previews; null when this chat takes none. */
const selections = props.block.selection ? usePluginSelection() : null
/** The selection this chat shows and sends: the one of the preview last selected on. */
const selection = computed(() => selections?.current.value ?? null)
/** The plugin store, which reads the history and the stream. */
const store = usePluginsStore()

/** Whether the chat sits in a split pane, which gives it its height on a wide screen. */
const inPane = usePluginPane()

/** The message field, which takes the focus back after a send. */
const composer = ref<{ focus: () => void } | null>(null)

/**
 * What a message carries once elements were selected for it: the claim goes in its body, and the message keeps the
 * claim, the chips and their preview so a "try again" can send the same.
 *
 * @param claim - The selected elements.
 * @param chips - Those elements as the plugin named them.
 * @param owner - The preview they were selected on.
 * @returns What the message carries.
 */
const carry = (claim: PluginSelectionClaim, chips: readonly PluginSelectionChip[], owner: string): PluginChatCarried => ({
  body: { selection: claim },
  mark: (message: PluginChatMessage): void => {
    message.selection = claim
    message.selectionChips = chips
    message.selectionOwner = owner
  },
})

/** The element selection of this chat, which spends it on a send and checks it again before a "try again". */
const selectionHooks: PluginChatSelectionHooks = {
  /**
   * Takes the selection into a send and ends it.
   *
   * @returns What the message carries, or undefined when nothing is selected.
   */
  spend: () => {
    const source = selection.value
    const claim = source?.claim() ?? undefined
    const chips = source ? [...source.chips.value] : []
    const owner = source?.owner ?? ''
    source?.clear()
    return claim ? carry(claim, chips, owner) : undefined
  },
  /**
   * Carries the selection of a message again.
   *
   * @param message - The message being sent again.
   * @returns What the message carries, undefined when it had no selection, null when its release changed.
   */
  repeat: (message) => {
    if (!message.selection) {
      return undefined
    }
    // Fails closed: a plugin reads a message with no selection as free to change anything, so dropping the claim would widen the edit.
    if (selections?.releaseOf(message.selectionOwner ?? '') !== message.selection.releaseId) {
      return null
    }
    return carry(message.selection, message.selectionChips ?? [], message.selectionOwner ?? '')
  },
}

/** The turns of the chat and the state the screen draws. */
const {
  messages, draft, log, following, error, retryable, selectionStale, streaming, loaded, canSend,
  isWriting, onScroll, jumpToLatest, send, pick, resendLast, stop,
} = usePluginChatTurn({
  /**
   * Reads the block this chat draws.
   *
   * @returns The block.
   */
  block: () => props.block,
  store,
  /**
   * Gives the message field the focus back.
   *
   * @returns Nothing.
   */
  focusField: () => composer.value?.focus(),
  selection: selectionHooks,
})

// The notice is about a selection the reader has since replaced once they select again; a refused retry sets it after, so it shows.
watch(() => selection.value?.chips.value, () => {
  selectionStale.value = false
})
</script>
