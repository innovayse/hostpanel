<template>
  <Transition name="slide-up">
    <div
      v-if="showBanner"
      class="fixed bottom-0 left-0 right-0 z-50 p-4 md:p-6 bg-[#0d0d12]/95 backdrop-blur-xl border-t-2 border-primary-500/30 shadow-2xl"
    >
      <div class="container-custom">
        <div class="flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
          <!-- Content -->
          <div class="flex-1 flex items-start gap-4">
            <div class="p-3 rounded-xl bg-primary-500/10 border border-primary-500/20">
              <Cookie :size="24" :stroke-width="2" class="text-primary-400" />
            </div>
            <div class="flex-1">
              <h3 class="text-lg font-bold text-white mb-2">
                {{ $t('cookieBanner.title') }}
              </h3>
              <p class="text-sm text-gray-400 leading-relaxed">
                {{ $t('cookieBanner.description') }}
                <NuxtLink
                  :to="localePath('/cookie-policy')"
                  class="text-primary-400 hover:text-primary-300 underline transition-colors"
                >
                  {{ $t('cookieBanner.learnMore') }}
                </NuxtLink>
              </p>
            </div>
          </div>

          <!-- Buttons -->
          <div class="flex flex-col sm:flex-row gap-3 w-full md:w-auto">
            <button
              @click="acceptEssential"
              class="px-6 py-3 rounded-xl font-medium text-gray-300 bg-white/5 border border-white/10 hover:bg-white/10 hover:border-white/20 transition-all duration-300 whitespace-nowrap"
            >
              {{ $t('cookieBanner.essentialOnly') }}
            </button>
            <button
              @click="acceptAll"
              class="px-6 py-3 rounded-xl font-medium text-on-primary bg-gradient-to-r from-primary-500 to-secondary-500 hover:from-primary-600 hover:to-secondary-600 shadow-lg shadow-primary-500/30 hover:shadow-primary-500/50 transition-all duration-300 whitespace-nowrap"
            >
              {{ $t('cookieBanner.acceptAll') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * Cookie consent banner component
 * Shows at the bottom of the page with options to accept essential or all cookies
 */

import { Cookie } from 'lucide-vue-next'

const localePath = useLocalePath()
const showBanner = ref(false)

// The choice lives in useCookieConsent, where plugins/tracking.client.ts reads it: this
// banner records the answer, the tag loader acts on it.
const { consent, accept } = useCookieConsent()

// Shown only until a choice is made. Mounted rather than computed from `consent` on the
// server, so a page never renders the banner into the HTML for a visitor who already chose.
onMounted(() => {
  if (!consent.value) showBanner.value = true
})

/** Accept essential cookies only: the tag manager is never loaded. */
const acceptEssential = () => {
  accept('essential')
  showBanner.value = false
}

/** Accept all cookies: the tag manager loads if the operator configured one. */
const acceptAll = () => {
  accept('all')
  showBanner.value = false
}
</script>

<style scoped>
.slide-up-enter-active,
.slide-up-leave-active {
  transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-up-enter-from,
.slide-up-leave-to {
  transform: translateY(100%);
  opacity: 0;
}

.slide-up-enter-to,
.slide-up-leave-from {
  transform: translateY(0);
  opacity: 1;
}
</style>
