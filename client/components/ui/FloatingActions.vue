<template>
  <!-- Floating Action Button — Peafowl fan-out -->
  <!--
    z-[60] rather than z-50: UiCookieBanner is a full-width bar fixed to the same
    corner at z-50, and being later in the document it took every click meant for
    this button until it was dismissed. The button stayed visible the whole time,
    which is the worst version of the problem — it looked live and was not.
  -->
  <div ref="fabRef" class="fixed bottom-6 right-6 z-[60]">

    <!-- Fan-out sub-buttons -->
    <div class="absolute bottom-0 right-0">

      <!-- Innochat — fans up-left (135°) -->
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
          class="group absolute bottom-0 right-0 flex items-center justify-center"
          style="transform: translate(-5px, -75px); transition-delay: 0ms;"
          :aria-label="$t('contact.info.quickConnect.liveChat')"
          @click.stop="openChat"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.liveChat') }}
          </span>
          <div
            class="w-10 h-10 bg-cyan-500 rounded-full flex items-center justify-center shadow-xl hover:scale-110 transition-transform duration-200"
            style="box-shadow: 0 4px 20px rgba(6,182,212,0.5);"
          >
            <MessageSquare :size="18" :stroke-width="2" class="text-white" />
          </div>
        </button>
      </Transition>

      <!-- WhatsApp — fans straight up (90°) -->
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
          :href="`https://wa.me/${whatsappNumber}?text=${encodeURIComponent(defaultMessage)}`"
          target="_blank"
          rel="noopener noreferrer"
          class="group absolute bottom-0 right-0 flex items-center justify-center"
          style="transform: translate(-55px, -55px); transition-delay: 60ms;"
          :aria-label="$t('whatsapp.contactUs')"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.whatsapp') }}
          </span>
          <div
            class="w-10 h-10 bg-[#25D366] rounded-full flex items-center justify-center shadow-xl hover:scale-110 transition-transform duration-200"
            style="box-shadow: 0 4px 20px rgba(37,211,102,0.5);"
          >
            <MessageCircle :size="18" :stroke-width="2" class="text-white" />
          </div>
        </a>
      </Transition>

      <!-- Telegram — fans left (180°) -->
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
          :href="`https://t.me/${telegramHandle}`"
          target="_blank"
          rel="noopener noreferrer"
          class="group absolute bottom-0 right-0 flex items-center justify-center"
          style="transform: translate(-75px, -5px); transition-delay: 120ms;"
          :aria-label="$t('telegram.contactUs')"
        >
          <span class="absolute right-full mr-2 px-2.5 py-1 bg-gray-900/90 text-white text-xs font-medium rounded-lg whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none">
            {{ $t('contact.info.quickConnect.telegram') }}
          </span>
          <div
            class="w-10 h-10 bg-[#0088cc] rounded-full flex items-center justify-center shadow-xl hover:scale-110 transition-transform duration-200"
            style="box-shadow: 0 4px 20px rgba(0,136,204,0.5);"
          >
            <Send :size="18" :stroke-width="2" class="text-white" />
          </div>
        </a>
      </Transition>

    </div>


    <!-- Main toggle button -->
    <button
      v-motion
      :initial="{ opacity: 0, scale: 0 }"
      :enter="{ opacity: 1, scale: 1, transition: { delay: 800, duration: 400, type: 'spring' } }"
      class="relative w-12 h-12 rounded-full flex items-center justify-center shadow-2xl transition-all duration-300 hover:scale-110 focus:outline-none"
      :class="isOpen ? 'bg-gray-700 rotate-45' : 'bg-gradient-to-br from-cyan-500 to-primary-600'"
      :style="isOpen ? '' : 'box-shadow: 0 4px 24px rgba(6,182,212,0.5);'"
      :aria-expanded="isOpen"
      :aria-label="$t('contact.info.quickConnect.toggle')"
      @click="isOpen = !isOpen"
    >
      <div v-if="!isOpen" class="absolute inset-0 rounded-full bg-cyan-500 animate-ping opacity-25" />
      <MessageCircle :size="26" :stroke-width="2" class="relative text-white transition-transform duration-300" :class="isOpen ? 'rotate-[-45deg]' : ''" />
    </button>

  </div>
</template>

<script setup lang="ts">
/**
 * Floating Action Button — Peafowl fan-out style
 * Main button fans out 3 sub-buttons in a 90° arc (up + left)
 */
import { Send, MessageCircle, MessageSquare } from 'lucide-vue-next'

const { t } = useI18n()

const fabRef = ref<HTMLElement | null>(null)
const isOpen = ref(false)

function handleOutsideClick(e: MouseEvent) {
  if (fabRef.value && !fabRef.value.contains(e.target as Node)) {
    isOpen.value = false
  }
}

/** Escape closes the fan-out, the keyboard equivalent of clicking away from it. */
function handleEscape(e: KeyboardEvent) {
  if (e.key === 'Escape') isOpen.value = false
}

onMounted(() => {
  document.addEventListener('click', handleOutsideClick)
  document.addEventListener('keydown', handleEscape)
})
onUnmounted(() => {
  document.removeEventListener('click', handleOutsideClick)
  document.removeEventListener('keydown', handleEscape)
})
const defaultMessage = computed(() => t('whatsapp.defaultMessage'))

// Contact channels come from the operator's settings rather than being baked in,
// so a self-hosted install shows its own contacts. Each action hides when unset.
const { get: getPortalSetting } = usePortalSettings()

/** WhatsApp number from configuration; empty hides the action. */
const whatsappNumber = computed(() => getPortalSetting('portal.contact.whatsapp', 'portalWhatsapp'))
/** Telegram handle from configuration; empty hides the action. */
const telegramHandle = computed(() => getPortalSetting('portal.contact.telegram', 'portalTelegram'))
/** Chat provider from configuration; empty hides the chat bubble. */
const chatProvider = computed(() => getPortalSetting('portal.chat.provider', 'portalChatProvider'))

/**
 * Whether to offer the live chat action.
 *
 * Both spellings are accepted. The provider is Innochat, which is what an
 * operator configuring this today would reasonably write, but the setting has
 * shipped seeded with `chatwoot` since the first release and existing installs
 * have that value stored. Refusing it would have switched the widget off for
 * every one of them on upgrade.
 */
const liveChatEnabled = computed(() =>
  ['chatwoot', 'innochat'].includes(chatProvider.value.toLowerCase()))

/**
 * Opens the live chat widget.
 *
 * The widget is started by the loader in app.vue and exposes itself on the
 * window; `openLiveChat` knows which global that is. It reports back rather
 * than failing silently, and a failure is logged: a visitor clicking a chat
 * button that does nothing has no way to tell that anything went wrong, and
 * neither did anyone reading the console.
 */
function openChat() {
  isOpen.value = false
  if (!import.meta.client) return

  if (!openLiveChat(window)) {
    console.warn('[live chat] widget is not running; the SDK has not started it yet')
  }
}
</script>
