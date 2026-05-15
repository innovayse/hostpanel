<template>
  <section class="py-20 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-tr from-secondary-500/5 via-transparent to-primary-500/5" />

      <!-- Glowing orbs -->
      <div class="absolute top-1/4 right-1/4 w-64 h-64 bg-secondary-500/15 rounded-full blur-[120px]" />
      <div class="absolute bottom-1/3 left-1/3 w-80 h-80 bg-primary-500/10 rounded-full blur-[140px]" />

      <!-- Floating shapes -->
      <div class="absolute top-1/3 left-1/4 w-2 h-2 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 0.7s;" />
      <div class="absolute bottom-1/4 right-1/3 w-3 h-3 bg-primary-400/40 rounded-full animate-float" style="animation-delay: 1.4s;" />

      <!-- Diagonal lines -->
      <div class="absolute top-0 left-1/4 w-px h-full bg-gradient-to-b from-transparent via-white/5 to-transparent" />
      <div class="absolute top-0 right-1/3 w-px h-full bg-gradient-to-b from-transparent via-white/5 to-transparent" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-16"
      >
        <span class="inline-block px-4 py-1.5 bg-secondary-500/20 text-secondary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('process.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
          {{ $t('process.heading') }} <span class="text-secondary-400">{{ $t('process.headingHighlight') }}</span>
        </h2>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('process.description') }}
        </p>
      </div>

      <!-- Process steps -->
      <div class="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div
          v-for="(step, index) in processSteps"
          :key="step.id"
          v-motion
          :initial="{ opacity: 0, y: 40 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 150, duration: 500 } }"
          class="relative group"
        >
          <!-- Step card -->
          <div class="relative h-full p-6 rounded-2xl bg-[#12121a] border border-gray-800 hover:border-secondary-500/50 transition-all duration-300 group-hover:-translate-y-2">
            <!-- Hover glow background -->
            <div class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-secondary-500/10 to-primary-500/5" />

            <!-- Step number with gradient -->
            <div class="relative w-14 h-14 rounded-full bg-gradient-to-br from-secondary-500 to-primary-500 flex items-center justify-center text-white font-bold text-lg mb-6 group-hover:scale-110 group-hover:rotate-12 transition-all duration-300 shadow-lg" :style="{ boxShadow: `0 0 30px ${index % 2 === 0 ? '#a855f7' : '#0ea5e9'}40` }">
              {{ String(step.order).padStart(2, '0') }}

              <!-- Pulse ring -->
              <div class="absolute inset-0 rounded-full border-2 border-white/30 opacity-0 group-hover:opacity-100 group-hover:scale-150 transition-all duration-500" />
            </div>

            <!-- Icon -->
            <Icon :name="step.icon" class="relative text-3xl text-secondary-400 mb-4 group-hover:scale-110 group-hover:rotate-6 transition-transform duration-300" />

            <!-- Content -->
            <h3 class="relative text-xl font-bold text-white mb-3 group-hover:text-secondary-300 transition-colors duration-300">{{ step.title }}</h3>
            <p class="relative text-gray-400 text-sm leading-relaxed">{{ step.description }}</p>

            <!-- Bottom accent -->
            <div class="absolute bottom-0 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-20 bg-gradient-to-r from-secondary-500 to-primary-500 transition-all duration-500 rounded-full" />

            <!-- Corner decoration with rounded border -->
            <div class="absolute top-0 right-0 w-12 h-12 border-r-2 border-t-2 border-secondary-500/50 rounded-tr-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
          </div>

          <!-- Connection line (hidden on mobile) -->
          <div
            v-if="index < processSteps.length - 1"
            class="hidden lg:block absolute top-10 -right-3 z-10"
          >
            <ArrowRight :size="24" :stroke-width="2" class="text-secondary-500/50" />
          </div>
        </div>
      </div>
    </div>

    <!-- Corner decorative accents -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Process section showing work methodology
 * Uses data from lib/data.ts with i18n translations
 */

import { ArrowRight } from 'lucide-vue-next'
import { processSteps as rawProcessSteps } from '~/lib/data'

const { t } = useI18n()

const processSteps = computed(() => rawProcessSteps.map(step => ({
  ...step,
  title: t(`process.steps.${step.id}.title`),
  description: t(`process.steps.${step.id}.description`)
})))
</script>
