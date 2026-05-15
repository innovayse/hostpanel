<template>
  <div class="min-h-screen bg-[#0a0a0f]">
    <!-- Hero Section -->
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

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto text-center">
          <span
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { duration: 500 } }"
            class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6"
          >
            {{ $t('aup.badge') }}
          </span>
          <h1
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 100, duration: 600 } }"
            class="text-4xl md:text-6xl font-bold text-white mb-6"
          >
            {{ $t('aup.title') }}
          </h1>
          <p
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
            class="text-xl text-gray-400 px-4"
          >
            {{ $t('aup.subtitle') }}
          </p>
          <p
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 300, duration: 500 } }"
            class="text-sm text-gray-500 mt-4"
          >
            {{ $t('aup.lastUpdated') }}: {{ lastUpdated }}
          </p>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Content Section -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden text-left">
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
            class="mb-16 last:mb-0"
          >
            <div class="flex items-center gap-4 mb-6">
              <div class="w-12 h-12 rounded-xl bg-primary-500/10 flex items-center justify-center text-primary-400 border border-primary-500/20">
                <Icon :name="section.icon" size="24" />
              </div>
              <h2 class="text-2xl md:text-3xl font-bold text-white">
                {{ $t(`aup.sections.${section.id}.title`) }}
              </h2>
            </div>

            <div class="pl-0 md:pl-16 space-y-4">
              <p
                v-for="pIndex in section.paragraphs"
                :key="pIndex"
                class="text-gray-400 leading-relaxed text-lg"
              >
                {{ $t(`aup.sections.${section.id}.content.${pIndex}`) }}
              </p>
            </div>
          </div>

          <!-- Contact / Report Section -->
          <div
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
            class="mt-20 p-8 md:p-12 rounded-3xl bg-gradient-to-br from-red-500/5 to-transparent border border-red-500/10 relative overflow-hidden group"
          >
            <div class="absolute top-0 right-0 w-64 h-64 bg-red-500/5 rounded-full blur-3xl -mr-32 -mt-32 group-hover:bg-red-500/10 transition-colors duration-500" />
            
            <div class="relative z-10">
              <h3 class="text-2xl font-bold text-white mb-4">{{ $t('aup.contact.title') }}</h3>
              <p class="text-gray-400 text-lg mb-8 max-w-2xl text-left">
                {{ $t('aup.contact.description') }}
              </p>
              <NuxtLink
                :to="localePath('/contact')"
                class="inline-flex items-center gap-3 px-8 py-4 bg-red-500/20 text-red-400 border border-red-500/30 rounded-xl font-semibold hover:bg-red-500/30 transition-all group/btn"
              >
                <ShieldAlert :size="20" />
                {{ $t('aup.contact.link') }}
                <ArrowRight :size="20" class="group-hover/btn:translate-x-1 transition-transform" />
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Acceptable Use Policy page
 */
import { ArrowRight, ShieldAlert } from 'lucide-vue-next'

const localePath = useLocalePath()
const lastUpdated = '2026-03-12'

const sections = [
  { id: 'purpose', icon: 'mdi:target-variant', paragraphs: [0] },
  { id: 'abuse', icon: 'mdi:alert-octagon-outline', paragraphs: [0, 1, 2, 3, 4] },
  { id: 'saas', icon: 'mdi:application-cog-outline', paragraphs: [0, 1, 2, 3] },
  { id: 'hosting', icon: 'mdi:server-security', paragraphs: [0, 1] },
  { id: 'enforcement', icon: 'mdi:gavel', paragraphs: [0, 1] }
]

useSeoMeta({
  title: 'Acceptable Use Policy | Innovayse',
  description: 'Understand the acceptable use policies for Innovayse hosting, SaaS products, and digital infrastructure.',
  robots: 'noindex, follow'
})
</script>

<style scoped>
.animate-blob {
  animation: blob 7s infinite;
}

@keyframes blob {
  0% { transform: translate(0px, 0px) scale(1); }
  33% { transform: translate(30px, -50px) scale(1.1); }
  66% { transform: translate(-20px, 20px) scale(0.9); }
  100% { transform: translate(0px, 0px) scale(1); }
}

.animation-delay-2000 {
  animation-delay: 2s;
}
</style>
