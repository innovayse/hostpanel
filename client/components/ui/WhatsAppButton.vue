<template>
  <!-- Floating Contact Buttons -->
  <div class="fixed bottom-6 right-6 z-50 flex flex-col gap-3">
    <!-- Telegram Button -->
    <a
      href="https://t.me/innovayse"
      target="_blank"
      rel="noopener noreferrer"
      class="group relative"
      :aria-label="$t('telegram.contactUs')"
    >
      <!-- Button with glow effect -->
      <div
        v-motion
        :initial="{ opacity: 0, scale: 0, rotate: -180 }"
        :enter="{ opacity: 1, scale: 1, rotate: 0, transition: { delay: 1100, duration: 500, type: 'spring' } }"
        class="relative w-12 h-12 md:w-14 md:h-14 bg-[#0088cc] rounded-full flex items-center justify-center shadow-xl hover:scale-110 transition-all duration-300 cursor-pointer"
        style="box-shadow: 0 4px 20px rgba(0, 136, 204, 0.4);"
      >
        <!-- Pulse animation -->
        <div class="absolute inset-0 rounded-full bg-[#0088cc] animate-ping opacity-75" />

        <!-- Telegram icon -->
        <Send
          :size="24"
          :stroke-width="2"
          fill="currentColor"
          class="relative text-white group-hover:scale-110 transition-transform md:hidden"
        />
        <Send
          :size="28"
          :stroke-width="2"
          fill="currentColor"
          class="relative text-white group-hover:scale-110 transition-transform hidden md:block"
        />
      </div>

      <!-- Tooltip (Hidden on mobile) -->
      <div
        class="absolute right-full mr-3 top-1/2 -translate-y-1/2 px-4 py-2 bg-gray-900 text-white text-sm font-medium rounded-lg whitespace-nowrap opacity-0 md:group-hover:opacity-100 transition-opacity duration-300 pointer-events-none hidden md:block"
        style="box-shadow: 0 4px 12px rgba(0,0,0,0.3);"
      >
        {{ $t('telegram.tooltip') }}
        <!-- Arrow -->
        <div class="absolute right-0 top-1/2 -translate-y-1/2 translate-x-full">
          <div class="w-0 h-0 border-t-8 border-t-transparent border-b-8 border-b-transparent border-l-8 border-l-gray-900" />
        </div>
      </div>
    </a>

    <!-- WhatsApp Button -->
    <a
      :href="`https://wa.me/${phoneNumber.replace(/[^0-9]/g, '')}?text=${encodeURIComponent(defaultMessage)}`"
      target="_blank"
      rel="noopener noreferrer"
      class="group relative"
      :aria-label="$t('whatsapp.contactUs')"
    >
      <!-- Button with glow effect -->
      <div
        v-motion
        :initial="{ opacity: 0, scale: 0, rotate: -180 }"
        :enter="{ opacity: 1, scale: 1, rotate: 0, transition: { delay: 1000, duration: 500, type: 'spring' } }"
        class="relative w-12 h-12 md:w-14 md:h-14 bg-[#25D366] rounded-full flex items-center justify-center shadow-xl hover:scale-110 transition-all duration-300 cursor-pointer"
        style="box-shadow: 0 4px 20px rgba(37, 211, 102, 0.4);"
      >
        <!-- Pulse animation -->
        <div class="absolute inset-0 rounded-full bg-[#25D366] animate-ping opacity-75" />

        <!-- WhatsApp icon -->
        <MessageCircle
          :size="24"
          :stroke-width="2"
          fill="currentColor"
          class="relative text-white group-hover:scale-110 transition-transform md:hidden"
        />
        <MessageCircle
          :size="28"
          :stroke-width="2"
          fill="currentColor"
          class="relative text-white group-hover:scale-110 transition-transform hidden md:block"
        />
      </div>

      <!-- Tooltip (Hidden on mobile) -->
      <div
        class="absolute right-full mr-3 top-1/2 -translate-y-1/2 px-4 py-2 bg-gray-900 text-white text-sm font-medium rounded-lg whitespace-nowrap opacity-0 md:group-hover:opacity-100 transition-opacity duration-300 pointer-events-none hidden md:block"
        style="box-shadow: 0 4px 12px rgba(0,0,0,0.3);"
      >
        {{ $t('whatsapp.tooltip') }}
        <!-- Arrow -->
        <div class="absolute right-0 top-1/2 -translate-y-1/2 translate-x-full">
          <div class="w-0 h-0 border-t-8 border-t-transparent border-b-8 border-b-transparent border-l-8 border-l-gray-900" />
        </div>
      </div>
    </a>
  </div>
</template>

<script setup lang="ts">
/**
 * Floating contact buttons (Telegram & WhatsApp)
 * Provides quick access to messaging platforms
 */

import { Send, MessageCircle } from 'lucide-vue-next'

const { t } = useI18n()

// WhatsApp phone number (international format without +)
const phoneNumber = '37433731673'

// Default message when opening WhatsApp
const defaultMessage = computed(() => t('whatsapp.defaultMessage'))
</script>

<style scoped>
@keyframes ping {
  75%, 100% {
    transform: scale(1.5);
    opacity: 0;
  }
}

.animate-ping {
  animation: ping 2s cubic-bezier(0, 0, 0.2, 1) infinite;
}
</style>
