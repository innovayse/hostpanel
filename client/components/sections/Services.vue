<template>
  <section class="py-20 bg-[#0d0d12] relative overflow-hidden">
    <!-- Background decorations -->
    <div class="absolute inset-0 pointer-events-none">
      <!-- Subtle gradient overlay -->
      <div class="absolute inset-0 bg-gradient-to-b from-transparent via-primary-500/5 to-transparent" />

      <!-- Soft glow -->
      <div class="absolute top-0 right-0 w-1/2 h-1/2 bg-primary-500/10 rounded-full blur-[150px]" />
      <div class="absolute bottom-0 left-0 w-1/3 h-1/3 bg-secondary-500/10 rounded-full blur-[120px]" />

      <!-- Floating shapes -->
      <div class="absolute top-20 left-1/4 w-2 h-2 bg-primary-400/50 rounded-full animate-float" />
      <div class="absolute top-40 right-1/3 w-3 h-3 bg-secondary-400/40 rounded-full animate-float" style="animation-delay: 1.5s;" />
      <div class="absolute bottom-32 right-1/4 w-2 h-2 bg-cyan-400/50 rounded-full animate-float" style="animation-delay: 0.8s;" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.015]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" />
      </div>
    </div>

    <div class="container-custom relative z-10">
      <!-- Section header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-16"
      >
        <span class="inline-block px-4 py-1 bg-primary-500/10 text-primary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('services.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
          {{ $t('services.title') }}
        </h2>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('services.subtitle') }}
        </p>
      </div>

      <!-- Services grid -->
      <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-8 mb-16">
        <div
          v-for="(service, index) in featuredServices"
          :key="service.id"
          v-motion
          :initial="{ opacity: 0, scale: 0.9, y: 50 }"
          :visibleOnce="{ 
            opacity: 1, 
            scale: 1, 
            y: 0, 
            transition: { 
              delay: index * 150, 
              duration: 800, 
              type: 'spring', 
              stiffness: 60 
            } 
          }"
          class="group relative rounded-[2rem] overflow-hidden cursor-pointer min-h-[480px] transition-all duration-500 hover:-translate-y-4 hover:shadow-[0_40px_80px_-20px_rgba(0,0,0,0.6)]"
          @click="navigateTo(localePath(service.link))"
        >
          <!-- Base background glass layer -->
          <div class="absolute inset-0 bg-white/[0.03] backdrop-blur-md" />

          <!-- Animated gradient border/glow -->
          <div 
            class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-700"
            :style="{ background: `linear-gradient(45deg, transparent, ${getServiceColor(index)}22, transparent)` }"
          />

          <!-- Large background icon (Parallax feel) -->
          <div class="absolute -bottom-10 -right-10 pointer-events-none transition-transform duration-1000 group-hover:-translate-x-12 group-hover:-translate-y-8">
            <Icon
              :name="`mdi:${service.icon}`"
              class="text-[240px] text-white/[0.03] group-hover:text-white/[0.07] transition-all duration-700 rotate-12"
            />
          </div>

          <!-- Card content -->
          <div class="relative p-10 h-full flex flex-col z-10">
            <!-- Icon box -->
            <div
              class="w-20 h-20 rounded-2xl flex items-center justify-center mb-10 transition-all duration-700 group-hover:scale-110 group-hover:rotate-[10deg] relative"
              :style="{
                backgroundColor: getServiceColor(index) + '15',
                borderColor: getServiceColor(index) + '30',
                boxShadow: `0 0 40px ${getServiceColor(index)}30`
              }"
            >
              <div class="absolute inset-0 rounded-2xl animate-pulse blur-xl opacity-0 group-hover:opacity-100" :style="{ backgroundColor: getServiceColor(index) + '20' }" />
              <Icon
                :name="`mdi:${service.icon}`"
                class="text-4xl relative z-10"
                :style="{ color: getServiceColor(index) }"
              />
            </div>

            <!-- Title -->
            <h3 class="text-3xl font-black text-white mb-4 group-hover:text-white transition-colors duration-300 tracking-tight">
              {{ service.title }}
            </h3>

            <!-- Description -->
            <p class="text-gray-400 text-lg mb-8 leading-relaxed font-light group-hover:text-gray-200 transition-colors duration-300">
              {{ service.description }}
            </p>

            <!-- Feature Checklist (Hidden by default, reveal on hover) -->
            <div class="space-y-3 mb-8 opacity-0 group-hover:opacity-100 translate-y-4 group-hover:translate-y-0 transition-all duration-500 delay-100">
              <div 
                v-for="(feature, fIndex) in service.features.slice(0, 4)" 
                :key="fIndex"
                class="flex items-center gap-3 text-sm text-gray-300 font-medium"
              >
                <div class="w-1.5 h-1.5 rounded-full" :style="{ backgroundColor: getServiceColor(index) }" />
                {{ feature }}
              </div>
            </div>

            <!-- CTA link -->
            <div class="mt-auto flex items-center justify-between pointer-events-none">
              <span
                class="inline-flex items-center text-lg font-bold transition-all duration-500 group-hover:translate-x-3"
                :style="{ color: getServiceColor(index) }"
              >
                {{ $t('services.learnMore') }}
                <ArrowRight :size="22" :stroke-width="3" class="ml-2" />
              </span>
              
              <div 
                class="w-14 h-14 rounded-full border border-white/10 flex items-center justify-center group-hover:border-white/20 transition-all"
                :style="{ backgroundColor: `${getServiceColor(index)}10` }"
              >
                <ArrowUpRight :size="24" :style="{ color: getServiceColor(index) }" />
              </div>
            </div>
          </div>

          <!-- Subtle outer border shimmer -->
          <div class="absolute inset-0 rounded-[2rem] border border-white/5 group-hover:border-white/20 transition-colors duration-500" />
        </div>
      </div>

      <!-- All Services button -->
      <div class="text-center">
        <UiButton
          variant="outline"
          size="lg"
          class="group border-gray-700 hover:border-primary-500 hover:shadow-lg hover:shadow-primary-500/20 transition-all duration-300"
          @click="navigateTo(localePath('/services'))"
        >
          <span>{{ $t('services.viewAll') }}</span>
          <ArrowRight :size="18" :stroke-width="2" class="ml-2 group-hover:translate-x-1 transition-transform" />
        </UiButton>
      </div>
    </div>

    <!-- Section corner decorations -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/20 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/20 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Services section with animated cards
 * Features: gradient backgrounds, mesh patterns, floating shapes,
 * animated particles, corner accents, glow effects
 */

import { ArrowRight, ArrowUpRight } from 'lucide-vue-next'
import { services as rawServices } from '~/lib/data'

const { t, tm } = useI18n()
const localePath = useLocalePath()

/** Featured services (first 3) with i18n translations */
const featuredServices = computed(() => rawServices.slice(0, 3).map(service => ({
  ...service,
  title: t(`services.items.${service.id}.title`),
  description: t(`services.items.${service.id}.description`),
  features: tm(`services.items.${service.id}.features`) as string[]
})))

/** Service gradient backgrounds */
const getServiceGradient = (index: number) => {
  const gradients = [
    'linear-gradient(135deg, #3b82f6 0%, #8b5cf6 50%, #d946ef 100%)',
    'linear-gradient(135deg, #06b6d4 0%, #0ea5e9 50%, #6366f1 100%)',
    'linear-gradient(135deg, #10b981 0%, #14b8a6 50%, #0891b2 100%)'
  ]
  return gradients[index % gradients.length]
}

/** Service accent colors */
const getServiceColor = (index: number) => {
  const colors = ['#a78bfa', '#22d3ee', '#34d399']
  return colors[index % colors.length]
}
</script>

<style scoped>
@keyframes float {
  0%, 100% {
    transform: translateY(0) scale(1);
    opacity: 0.4;
  }
  50% {
    transform: translateY(-20px) scale(1.3);
    opacity: 1;
  }
}

.animate-float {
  animation: float 3s ease-in-out infinite;
}
</style>
