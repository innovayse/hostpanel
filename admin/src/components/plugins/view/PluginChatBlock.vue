<script setup lang="ts">
/**
 * A chat with the plugin: earlier messages read from the `history` GET route, and each message sent to
 * the streaming `send` route, whose events are shown as they arrive: `delta` adds text to the reply,
 * `message` replaces it, `status` says what the plugin is doing, `error` shows the sentence (the plugin's
 * `text`, or the host's localised `message` when the plugin failed mid-stream), `link` (`{ text, url }`) adds a
 * link when the link rule admits the address, `done` ends the reply. `step` keeps a compact list inside the
 * reply, a repeated key updating its row; `result` adds a card with actions under the same link and route
 * rules as other blocks; `issue` shows the plugin's own failure while the stream goes on. A malformed event
 * is ignored whole. Everything is text: a reply is read as Markdown through `UiMarkdown` with its links kept
 * as text, because an address renders only through the link rule.
 * The events become messages in `usePluginChatMessages`; the message, the empty state, the composer and the
 * step list are their own components (PluginChatMessage, PluginChatEmpty, PluginChatComposer, PluginChatSteps).
 *
 * The stream is read through the store, which opens it with `fetch` (never `EventSource`) with the CSRF
 * and language headers. Stop and leaving the page end it, and the plugin's call ends with it; after a Stop
 * a step still running shows as stopped and no failure is shown.
 *
 * The log (`role="log"`) scrolls on its own and follows the reply, unless the operator scrolled up, when
 * "Jump to latest" offers the way back (`usePluginChatScroll`). Enter sends, Shift+Enter writes a new line, and
 * the field takes the focus again after every send.
 *
 * The unsent draft is kept in the tab's session storage (`usePluginChatDraft`) and forgotten once a reply
 * arrived, or on sign-out; leaving the page, or a Stop before the first word, keeps it. A send that got no reply
 * (it failed, was stopped, or went to a route the page cannot call) gives its text back to the field and is not
 * left shown as sent; a failed turn with a reply keeps its messages and offers "Retry", which replaces the failed
 * turn with a send of the same message. The turns themselves are `usePluginChatTurn`, the same in both apps.
 *
 * An empty chat shows the block's `intro` and its `suggestions`, which send their text when pressed;
 * `assistant.label` signs each reply, `maxLength` caps the field. Once a reply ended, successfully or not, the
 * blocks named in `refresh` read again. A refresh of this block's own `id` (a "new conversation" action)
 * stops the reply, clears the messages and reads the history again.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginChatComposer from '@/components/plugins/view/PluginChatComposer.vue'
import PluginChatEmpty from '@/components/plugins/view/PluginChatEmpty.vue'
import PluginChatMessage from '@/components/plugins/view/PluginChatMessage.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { usePluginChatTurn } from '@/composables/usePluginChatTurn'
import { usePluginPane } from '@/composables/usePluginView'
import { Icon } from '@/lib/icons'
import { usePluginUiStore } from '@/stores/pluginUi'
import type { PluginChatBlock } from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Props for PluginChatBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginChatBlock
}>()

/** Translation function. */
const { t } = useI18n()
/** The plugin ui store, which reads the history and the stream. */
const store = usePluginUiStore()
/** Whether the chat sits in a split pane, which bounds its log lower. */
const inPane = usePluginPane()

/** The message field, which takes the focus back after a send. */
const composer = ref<{ focus: () => void } | null>(null)

/** The turns of the chat and the state the screen draws; the admin has no element selection to supply. */
const {
  messages, draft, log, following, error, retryable, streaming, loaded, canSend,
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
})

/** The height bound of the log: lower inside a split pane. */
const logClasses = computed<string>(() => (inPane ? 'max-h-[50dvh]' : 'max-h-[60dvh]'))
</script>

<template>
  <UiCard padding="md" data-testid="plugin-chat">
    <UiStack :gap="UiGap.Sm">
      <!-- The log scrolls by itself, so the composer below it stays in view. -->
      <UiStack :gap="UiGap.None" class="relative">
        <div
          ref="log"
          role="log"
          tabindex="0"
          aria-live="polite"
          :aria-busy="streaming"
          :aria-label="t('plugins.pluginPage.chatLog')"
          class="min-h-48 overflow-y-auto"
          :class="logClasses"
          data-testid="plugin-chat-log"
          @scroll="onScroll"
        >
          <PluginChatEmpty
            v-if="loaded && messages.length === 0"
            :intro="block.intro"
            :placeholder="block.placeholder ?? t('plugins.pluginPage.chatEmpty')"
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
            <UiAlert v-if="error" :variant="UiTone.Danger">
              <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm" :align="UiAlignItems.Center" :justify="UiJustify.Between">
                <UiText :size="UiTextSize.Sm" data-testid="plugin-chat-error">{{ error }}</UiText>
                <UiButton
                  v-if="retryable"
                  :variant="UiButtonVariant.Secondary"
                  :size="UiSize.Sm"
                  :disabled="streaming"
                  data-testid="plugin-chat-retry"
                  @click="resendLast(true)"
                >{{ t('plugins.pluginPage.chatRetry') }}</UiButton>
              </UiStack>
            </UiAlert>
          </UiStack>
        </div>
        <UiStack v-if="!following" :align="UiAlignItems.Center" class="pointer-events-none absolute inset-x-0 bottom-2">
          <UiButton
            :variant="UiButtonVariant.Secondary"
            :size="UiSize.Sm"
            class="pointer-events-auto shadow"
            data-testid="plugin-chat-jump"
            @click="jumpToLatest"
          >
            <UiIcon :markup="Icon.ArrowDown" :size="UiSize.Sm" />
            {{ t('plugins.pluginPage.chatJumpToLatest') }}
          </UiButton>
        </UiStack>
      </UiStack>
      <PluginChatComposer
        ref="composer"
        v-model="draft"
        :placeholder="block.placeholder ?? t('plugins.pluginPage.chatMessage')"
        :max-length="block.maxLength"
        :streaming="streaming"
        :can-send="canSend"
        @send="send"
        @stop="stop"
      />
    </UiStack>
  </UiCard>
</template>
