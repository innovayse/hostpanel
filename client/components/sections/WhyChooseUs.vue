<template>
  <section class="py-20 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-br from-primary-500/5 via-transparent to-cyan-500/5" />
      <div class="absolute top-1/2 left-0 w-1/3 h-1/2 bg-primary-500/10 rounded-full blur-[150px]" />
      <div class="absolute bottom-0 right-0 w-1/4 h-1/3 bg-secondary-500/8 rounded-full blur-[120px]" />

      <!-- Floating particles -->
      <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-primary-400/50 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute top-1/3 left-1/4 w-3 h-3 bg-secondary-400/40 rounded-full animate-float" style="animation-delay: 1.2s;" />
      <div class="absolute bottom-1/3 right-1/3 w-2 h-2 bg-cyan-400/50 rounded-full animate-float" style="animation-delay: 2s;" />

      <!-- Grid overlay -->
      <div class="absolute inset-0 opacity-[0.015]">
        <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" class="w-full h-full" />
      </div>
    </div>

    <div class="container-custom relative z-10">
      <!-- Header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-16"
      >
        <span class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('whyChooseUs.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-6" v-html="$t('whyChooseUs.title', { results: `<span class='text-primary-400'>${$t('whyChooseUs.titleHighlight')}</span>` })" />
        <p class="text-lg text-gray-400 mb-8 max-w-2xl mx-auto">
          {{ $t('whyChooseUs.subtitle') }}
        </p>
      </div>

      <!-- Advantages grid -->
      <div class="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div
          v-for="(advantage, index) in advantages"
          :key="advantage.title"
          v-motion
          :initial="{ opacity: 0, y: 40 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 500 } }"
          class="relative flex flex-col items-center text-center p-6 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 group hover:-translate-y-2"
        >
          <!-- Glow background on hover -->
          <div
            class="absolute inset-0 rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-cyan-500/5"
          />

          <!-- Icon with animated background -->
          <div class="relative w-16 h-16 rounded-xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 flex items-center justify-center mb-4 group-hover:scale-110 transition-transform duration-300">
            <!-- Pulse ring -->
            <div class="absolute inset-0 rounded-xl border border-primary-500/30 opacity-0 group-hover:opacity-100 group-hover:scale-125 transition-all duration-500" />

            <Icon :name="advantage.icon" class="relative text-3xl text-primary-400 group-hover:rotate-12 transition-transform duration-300" />
          </div>

          <!-- Content -->
          <h3 class="relative text-lg font-semibold text-white mb-2">{{ advantage.title }}</h3>
          <p class="relative text-sm text-gray-400 leading-relaxed">{{ advantage.description }}</p>

          <!-- Bottom accent line -->
          <div class="absolute bottom-0 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-16 bg-gradient-to-r from-primary-500 to-cyan-500 rounded-full transition-all duration-500" />

          <!-- Corner accent with rounded border -->
          <div class="absolute top-0 left-0 w-10 h-10 border-l-2 border-t-2 border-primary-500/50 rounded-tl-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
        </div>
      </div>
    </div>

    <!-- Section decorative corners -->
    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/25 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/25 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Why Choose Us section highlighting company advantages
 */

const { t } = useI18n()

const advantages = computed(() => [
  {
    icon: 'mdi:rocket-launch',
    title: t('whyChooseUs.advantages.fastDelivery.title'),
    description: t('whyChooseUs.advantages.fastDelivery.description')
  },
  {
    icon: 'mdi:shield-check',
    title: t('whyChooseUs.advantages.qualityGuaranteed.title'),
    description: t('whyChooseUs.advantages.qualityGuaranteed.description')
  },
  {
    icon: 'mdi:headset',
    title: t('whyChooseUs.advantages.support.title'),
    description: t('whyChooseUs.advantages.support.description')
  },
  {
    icon: 'mdi:trending-up',
    title: t('whyChooseUs.advantages.provenResults.title'),
    description: t('whyChooseUs.advantages.provenResults.description')
  }
])
</script>
