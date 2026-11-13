<template>
  <!--
    z-[60] rather than z-50: UiCookieBanner is a full-width bar fixed to the same
    corner at z-50, and being later in the document it took every click meant for
    this button until it was dismissed. The button stayed visible the whole time,
    which is the worst version of the problem: it looked live and was not.
  -->
  <div ref="fabRef" class="fixed bottom-[calc(1.5rem+var(--cookie-banner-height,0px))] right-6 z-[60]" data-testid="floating-actions">

    <!-- Fan-out sub-buttons -->
    <div class="absolute bottom-0 right-0">

      <!-- Live chat: fans up-left -->
      <Transition
        enter-active-class="transition-all duration-300 ease-out"
        leave-active-class="transition-all duration-200 ease-in"
        enter-from-class="opacity-0 scale-0"
        enter-to-class="opacity-100 scale-100"
        leave-from-class="opacity-100 scale-100"
        leave-to-class="opacity-0 scale-0"
      >
        <button
          v-if="isOpen && liveChatEnabled"
          type="button"
          data-testid="floating-live-chat"
          :class="FOCUS_RING"
          class="group absolute bottom-0 right-0 rounded-full flex items-center justify-center -translate-x-[5px] -translate-y-[75px] delay-0"
          :aria-label="$t('contact.info.quickConnect.liveChat')"
          @click.stop="openChat"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 dark:bg-gray-800/95 dark:ring-1 dark:ring-white/15 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.liveChat') }}
          </span>
          <span class="w-10 h-10 bg-primary-600 text-on-primary dark:bg-primary-500 rounded-full flex items-center justify-center shadow-lg shadow-primary-500/40 hover:scale-110 transition-transform duration-200">
            <MessageSquare :size="18" :stroke-width="2" />
          </span>
        </button>
      </Transition>

      <!-- WhatsApp: fans straight up -->
      <Transition
        enter-active-class="transition-all duration-300 ease-out"
        leave-active-class="transition-all duration-200 ease-in"
        enter-from-class="opacity-0 scale-0"
        enter-to-class="opacity-100 scale-100"
        leave-from-class="opacity-100 scale-100"
        leave-to-class="opacity-0 scale-0"
      >
        <a
          v-if="isOpen && whatsappNumber"
          :href="`${WHATSAPP_LINK_BASE}${whatsappNumber}?text=${encodeURIComponent(defaultMessage)}`"
          target="_blank"
          rel="noopener noreferrer"
          data-testid="floating-whatsapp"
          :class="FOCUS_RING"
          class="group absolute bottom-0 right-0 rounded-full flex items-center justify-center -translate-x-[55px] -translate-y-[55px] delay-[60ms]"
          :aria-label="$t('whatsapp.contactUs')"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 dark:bg-gray-800/95 dark:ring-1 dark:ring-white/15 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.whatsapp') }}
          </span>
          <span class="w-10 h-10 bg-channel-whatsapp rounded-full flex items-center justify-center shadow-glow-whatsapp hover:scale-110 transition-transform duration-200">
            <MessageCircle :size="18" :stroke-width="2" class="text-white" />
          </span>
        </a>
      </Transition>

      <!-- Telegram: fans left -->
      <Transition
        enter-active-class="transition-all duration-300 ease-out"
        leave-active-class="transition-all duration-200 ease-in"
        enter-from-class="opacity-0 scale-0"
        enter-to-class="opacity-100 scale-100"
        leave-from-class="opacity-100 scale-100"
        leave-to-class="opacity-0 scale-0"
      >
        <a
          v-if="isOpen && telegramHandle"
          :href="`${TELEGRAM_LINK_BASE}${telegramHandle}`"
          target="_blank"
          rel="noopener noreferrer"
          data-testid="floating-telegram"
          :class="FOCUS_RING"
          class="group absolute bottom-0 right-0 rounded-full flex items-center justify-center -translate-x-[75px] -translate-y-[5px] delay-[120ms]"
          :aria-label="$t('telegram.contactUs')"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 dark:bg-gray-800/95 dark:ring-1 dark:ring-white/15 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.telegram') }}
          </span>
          <span class="w-10 h-10 bg-channel-telegram rounded-full flex items-center justify-center shadow-glow-telegram hover:scale-110 transition-transform duration-200">
            <Send :size="18" :stroke-width="2" class="text-white" />
          </span>
        </a>
      </Transition>

    </div>

    <!-- Main toggle button -->
    <button
      type="button"
      data-testid="floating-toggle"
      class="relative w-12 h-12 rounded-full flex items-center justify-center shadow-2xl transition-all duration-300 hover:scale-110 animate-reveal-up motion-reduce:animate-none"
      :class="[FOCUS_RING, isOpen ? 'bg-gray-700 text-white rotate-45' : 'bg-primary-600 text-on-primary shadow-primary-500/40 hover:bg-primary-700 dark:bg-primary-500 dark:hover:bg-primary-400', revealDelay(800)]"
      :aria-expanded="isOpen"
      :aria-label="$t('contact.info.quickConnect.toggle')"
      @click="isOpen = !isOpen"
    >
      <span v-if="!isOpen" class="absolute inset-0 rounded-full bg-primary-500 animate-ping opacity-25" />
      <MessageCircle :size="26" :stroke-width="2" class="relative transition-transform duration-300" :class="isOpen ? 'rotate-[-45deg]' : ''" />
    </button>

  </div>
</template>

<script setup lang="ts">
/**
 * FloatingActions (`UiFloatingActions`): the floating contact button that fans out three actions
 * (live chat, WhatsApp, Telegram) up and to the left of the bottom-right corner.
 *
 * Contact channels come from the operator's settings rather than being baked in, so a self-hosted
 * install shows its own contacts; each action hides when its setting is empty. Escape or a click
 * outside closes the fan.
 */

import { revealDelay } from '~/utils/reveal'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { TELEGRAM_LINK_BASE, WHATSAPP_LINK_BASE } from '~/constants/contactChannel'
import { ChatProvider } from '~/types/chatprovider'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { Send, MessageCircle, MessageSquare } from 'lucide-vue-next'
import { FOCUS_RING } from '~/utils/ui/tones'

/** Translator for the strings this component builds itself. */
const { t } = useI18n()

/** The root element, used to detect outside clicks. */
const fabRef = ref<HTMLElement | null>(null)

/** Whether the fan is open. */
const isOpen = ref(false)

/**
 * Closes the fan when a click lands outside the component.
 *
 * @param event The document click.
 */
const handleOutsideClick = (event: MouseEvent): void => {
  if (fabRef.value && !fabRef.value.contains(event.target as Node)) {
    isOpen.value = false
  }
}

/**
 * Escape closes the fan, the keyboard equivalent of clicking away from it.
 *
 * @param event The document key press.
 */
const handleEscape = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    isOpen.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', handleOutsideClick)
  document.addEventListener('keydown', handleEscape)
})
onUnmounted(() => {
  document.removeEventListener('click', handleOutsideClick)
  document.removeEventListener('keydown', handleEscape)
})

/** Message pre-filled when WhatsApp opens. */
const defaultMessage = computed(() => t('whatsapp.defaultMessage'))

/** Reads an operator setting, falling back to its environment value. */
const { get: getPortalSetting } = usePortalSettings()

/** WhatsApp number from configuration; empty hides the action. */
const whatsappNumber = computed(() => getPortalSetting(PortalSettingKey.ContactWhatsapp, 'portalWhatsapp'))

/** Telegram handle from configuration; empty hides the action. */
const telegramHandle = computed(() => getPortalSetting(PortalSettingKey.ContactTelegram, 'portalTelegram'))

/** Chat provider from configuration; empty hides the chat bubble. */
const chatProvider = computed(() => getPortalSetting(PortalSettingKey.ChatProvider, 'portalChatProvider'))

/**
 * Whether to offer the live chat action.
 *
 * Every {@link ChatProvider} value is accepted, the one the setting has been seeded with since
 * the first release included: existing installs have it stored, and refusing it would switch
 * the widget off for every one of them on upgrade.
 */
const liveChatEnabled = computed(() =>
  Object.values<string>(ChatProvider).includes(chatProvider.value.toLowerCase()))

/**
 * Opens the live chat widget.
 *
 * The widget is started by the loader in app.vue and exposes itself on the window; `openLiveChat`
 * knows which global that is. It reports back rather than failing silently, and a failure is
 * logged: a visitor clicking a chat button that does nothing has no way to tell that anything
 * went wrong, and neither did anyone reading the console.
 */
const openChat = (): void => {
  isOpen.value = false
  if (!import.meta.client) {
    return
  }

  if (!openLiveChat(window)) {
    console.warn('[live chat] widget is not running; the SDK has not started it yet')
  }
}
</script>
