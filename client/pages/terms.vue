<template>
  <div>
    <!-- Hero -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :enter="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="container-custom relative z-10 text-center"
      >
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6">
          {{ $t('terms.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl font-bold text-white mb-6 break-words">
          {{ $t('terms.title') }}
        </h1>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('terms.subtitle') }}
        </p>
        <p class="text-sm text-gray-500 mt-4">
          {{ $t('terms.lastUpdated') }}: {{ lastUpdated }}
        </p>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Terms Content -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <div
            v-for="(section, index) in sections"
            :key="section.id"
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
            class="mb-12"
          >
            <h2 class="text-2xl md:text-3xl font-bold text-white mb-6 flex flex-col md:flex-row items-center gap-3 text-center md:text-left">
              <span class="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-500/20 to-secondary-500/10 flex items-center justify-center text-primary-400 text-lg font-bold border border-primary-500/20">
                {{ index + 1 }}
              </span>
              {{ $t(`terms.sections.${section.id}.title`) }}
            </h2>
            <div class="pl-13 space-y-4">
              <p
                v-for="(paragraph, pIndex) in section.paragraphs"
                :key="pIndex"
                class="text-gray-400 leading-relaxed"
              >
                {{ $t(`terms.sections.${section.id}.content.${pIndex}`) }}
              </p>
            </div>
          </div>

          <!-- Contact Section -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
            class="mt-16 p-8 rounded-2xl bg-white/5 border border-white/10"
          >
            <h3 class="text-xl font-bold text-white mb-4">{{ $t('terms.contact.title') }}</h3>
            <p class="text-gray-400 mb-4">{{ $t('terms.contact.description') }}</p>
            <NuxtLink
              :to="localePath('/contact')"
              class="inline-flex items-center gap-2 text-primary-400 hover:text-primary-300 transition-colors font-medium"
            >
              {{ $t('terms.contact.link') }}
              <ArrowRight :size="18" :stroke-width="2" />
            </NuxtLink>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Terms of Service page
 */

import { ArrowRight } from 'lucide-vue-next'

const localePath = useLocalePath()
const lastUpdated = '2026-03-12'

const sections = [
  { id: 'acceptance', paragraphs: [0, 1] },
  { id: 'services', paragraphs: [0, 1, 2] },
  { id: 'accounts', paragraphs: [0, 1] },
  { id: 'intellectual', paragraphs: [0, 1, 2] },
  { id: 'payment', paragraphs: [0, 1, 2] },
  { id: 'termination', paragraphs: [0, 1] },
  { id: 'liability', paragraphs: [0, 1] },
  { id: 'changes', paragraphs: [0, 1] }
]

useSeoMeta({
  title: 'Terms of Service | Innovayse',
  description: 'Read our Terms of Service to understand the rules and guidelines for using Innovayse services and products.',
  robots: 'noindex, follow'
})
</script>

<style scoped>
.pl-13 {
  padding-left: 3.25rem;
}
</style>
