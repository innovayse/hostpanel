<template>
  <section class="py-10 sm:py-16 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-b from-transparent via-white/[0.02] to-transparent" />

      <!-- Floating shapes -->
      <div class="absolute top-10 left-10 w-2 h-2 bg-primary-400/40 rounded-full animate-float" />
      <div class="absolute top-20 right-20 w-3 h-3 bg-secondary-400/30 rounded-full animate-float" style="animation-delay: 1s;" />
      <div class="absolute bottom-10 left-1/3 w-2 h-2 bg-cyan-400/40 rounded-full animate-float" style="animation-delay: 2s;" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 20 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
        class="text-center mb-8 sm:mb-12"
      >
        <p class="text-gray-500 text-sm uppercase tracking-wider mb-2">{{ $t('clients.subtitle') }}</p>
        <h3 class="text-xl font-semibold text-white">{{ $t('clients.heading') }}</h3>
      </div>

      <!-- Logos marquee -->
      <div class="relative overflow-hidden">
        <!-- Fade edges -->
        <div class="absolute left-0 top-0 bottom-0 w-20 bg-gradient-to-r from-[#0a0a0f] to-transparent z-10" />
        <div class="absolute right-0 top-0 bottom-0 w-20 bg-gradient-to-l from-[#0a0a0f] to-transparent z-10" />

        <!-- Scrolling logos -->
        <div class="flex animate-marquee py-4">
          <div
            v-for="(partner, index) in [...clients, ...clients]"
            :key="index"
            class="flex-shrink-0 mx-3 sm:mx-6 flex items-center justify-center"
          >
            <div class="px-4 py-2.5 sm:px-6 sm:py-3 rounded-lg bg-white/5 border border-white/10 hover:border-primary-500/50 hover:bg-white/10 transition-all duration-300 group hover:scale-105 hover:shadow-xl hover:shadow-primary-500/30">
              <div class="flex items-center gap-2">
                <Icon :name="partner.icon" class="text-xl sm:text-2xl text-gray-500 group-hover:text-primary-400 transition-colors duration-300 flex-shrink-0" />
                <span class="text-sm sm:text-base font-semibold text-gray-500 group-hover:text-white transition-colors duration-300 whitespace-nowrap">{{ partner.name }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Stats bar -->
      <div class="mt-8 sm:mt-12 grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-6">
        <div
          v-for="(stat, index) in partnerStats"
          :key="stat.label"
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 400 } }"
          class="relative text-center p-4 rounded-xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 group hover:-translate-y-1"
        >
          <!-- Glow effect on hover -->
          <div class="absolute inset-0 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-secondary-500/10" />

          <div class="relative">
            <div class="text-2xl md:text-3xl font-bold text-white mb-1 group-hover:scale-110 transition-transform duration-300">{{ stat.value }}</div>
            <div class="text-xs text-gray-500 uppercase tracking-wider">{{ stat.label }}</div>
          </div>

          <!-- Corner accent with rounded border -->
          <div class="absolute top-0 right-0 w-8 h-8 border-r-2 border-t-2 border-primary-500/40 rounded-tr-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
        </div>
      </div>
    </div>

    <!-- Decorative corner accents -->
    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Partners section with marquee animation
 * Uses data from lib/data.ts
 */

import { clients } from '~/lib/data'

const { t } = useI18n()

const partnerStats = computed(() => [
  { value: '50+', label: t('clients.stats.clients') },
  { value: '15+', label: t('clients.stats.countries') },
  { value: '$10M+', label: t('clients.stats.revenue') },
  { value: '99%', label: t('clients.stats.retention') }
])
</script>

<style scoped>
@keyframes marquee {
  0% {
    transform: translateX(0);
  }
  100% {
    transform: translateX(-50%);
  }
}

.animate-marquee {
  animation: marquee 30s linear infinite;
}

.animate-marquee:hover {
  animation-play-state: paused;
}
</style>
