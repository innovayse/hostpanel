<template>
  <section class="py-20 bg-[#0a0a0f] relative overflow-hidden">
    <!-- Background decorations -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      <div class="absolute top-0 left-0 w-1/2 h-1/2 bg-primary-500/10 rounded-full blur-[150px]" />
      <div class="absolute bottom-0 right-0 w-1/3 h-1/3 bg-secondary-500/10 rounded-full blur-[120px]" />

      <!-- Floating stars -->
      <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.8s;" />
      <div class="absolute bottom-1/3 left-1/4 w-3 h-3 bg-secondary-400/50 rounded-full animate-float" style="animation-delay: 1.5s;" />
      <div class="absolute top-2/3 right-1/3 w-2 h-2 bg-cyan-400/60 rounded-full animate-float" style="animation-delay: 2.3s;" />

      <!-- Decorative circles -->
      <div class="absolute top-1/2 left-10 w-32 h-32 rounded-full border border-white/5" />
      <div class="absolute bottom-1/4 right-20 w-24 h-24 rounded-full border border-white/5" />
    </div>

    <div class="container-custom relative z-10">
      <!-- Section header -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 30 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="text-center mb-16"
      >
        <span class="inline-block px-4 py-1.5 bg-secondary-500/20 text-secondary-400 text-sm font-medium rounded-full mb-4">
          {{ $t('testimonials.badge') }}
        </span>
        <h2 class="text-3xl md:text-5xl font-bold text-white mb-4">
          {{ $t('testimonials.heading') }} <span class="text-secondary-400">{{ $t('testimonials.headingHighlight') }}</span> {{ $t('testimonials.headingEnd') }}
        </h2>
        <p class="text-lg text-gray-400 max-w-2xl mx-auto">
          {{ $t('testimonials.subtitle') }}
        </p>
      </div>

      <!-- Swiper carousel -->
      <div class="relative max-w-4xl mx-auto">
        <ClientOnly>
          <Swiper
            :modules="modules"
            :slides-per-view="1"
            :space-between="30"
            :loop="true"
            :autoplay="{
              delay: 5000,
              disableOnInteraction: false,
              pauseOnMouseEnter: true
            }"
            :pagination="{
              clickable: true
            }"
            class="testimonials-swiper"
          >
            <SwiperSlide
              v-for="(testimonial, index) in displayedTestimonials"
              :key="testimonial.id"
            >
              <!-- Card -->
              <div class="relative rounded-2xl overflow-hidden">
                <!-- Gradient background -->
                <div
                  class="absolute inset-0"
                  :style="{ background: getTestimonialGradient(index) }"
                />

                <!-- Animated mesh pattern -->
                <div class="absolute inset-0 opacity-10">
                  <div style="background-image: radial-gradient(circle, rgba(255,255,255,0.2) 1px, transparent 1px); background-size: 30px 30px;" class="w-full h-full" />
                </div>

                <!-- Content -->
                <div class="relative p-8 md:p-10">
                  <!-- Rating stars -->
                  <div class="flex items-center gap-1 mb-6">
                    <Star
                      v-for="star in 5"
                      :key="star"
                      :size="20"
                      :stroke-width="2"
                      fill="currentColor"
                      class="text-primary-400 animate-pulse"
                      :style="{ animationDelay: `${star * 0.1}s` }"
                    />
                  </div>

                  <!-- Quote icon decoration -->
                  <div class="absolute top-6 right-8 text-white/5">
                    <Quote :size="64" :stroke-width="2" />
                  </div>

                  <!-- Testimonial text -->
                  <p class="relative text-lg md:text-xl text-white/90 mb-8 leading-relaxed italic">
                    "{{ testimonial.text }}"
                  </p>

                  <!-- Author info -->
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-4">
                      <!-- Avatar with ring -->
                      <div class="relative w-14 h-14 rounded-full border-2 border-primary-400 p-0.5">
                        <!-- Animated ring -->
                        <div class="absolute inset-0 rounded-full border-2 border-primary-500/50 animate-ping" />

                        <div class="relative w-full h-full rounded-full bg-gray-900 flex items-center justify-center text-white font-bold text-sm">
                          {{ getInitials(testimonial.name) }}
                        </div>
                      </div>

                      <div>
                        <div class="font-semibold text-white">
                          {{ testimonial.name }}
                        </div>
                        <div class="text-sm text-gray-300">
                          {{ testimonial.position }}
                        </div>
                        <div class="text-sm text-secondary-400">
                          {{ testimonial.company }}
                        </div>
                      </div>
                    </div>

                    <!-- Company badge -->
                    <div class="hidden md:flex items-center gap-2 px-4 py-2 bg-white/10 backdrop-blur-sm rounded-lg border border-white/20 hover:border-primary-500/50 transition-colors duration-300">
                      <Building2 :size="18" :stroke-width="2" class="text-gray-300" />
                      <span class="text-sm text-gray-300">{{ testimonial.company }}</span>
                    </div>
                  </div>
                </div>

                <!-- Corner decorations with rounded borders -->
                <div class="absolute top-0 left-0 w-14 h-14 border-l-2 border-t-2 border-primary-500/40 rounded-tl-xl" />
                <div class="absolute bottom-0 right-0 w-14 h-14 border-r-2 border-b-2 border-secondary-500/40 rounded-br-xl" />
              </div>
            </SwiperSlide>
          </Swiper>
        </ClientOnly>

        <!-- Swipe hint -->
        <div class="flex justify-center items-center mt-6">
          <span class="text-sm text-gray-500">{{ $t('testimonials.hint') }}</span>
        </div>
      </div>

      <!-- Stats row -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4 sm:gap-6 mt-12 sm:mt-14 md:mt-16 max-w-4xl mx-auto">
        <div
          v-for="(stat, index) in testimonialStats"
          :key="stat.label"
          v-motion
          :initial="{ opacity: 0, y: 30 }"
          :visibleOnce="{ opacity: 1, y: 0, transition: { delay: index * 100, duration: 400 } }"
          class="relative text-center p-3 sm:p-4 md:p-6 rounded-lg sm:rounded-xl bg-white/5 backdrop-blur-sm border border-white/10 hover:border-secondary-500/30 transition-all duration-300 group hover:-translate-y-1 min-w-0"
        >
          <!-- Glow on hover -->
          <div class="absolute inset-0 rounded-lg sm:rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 bg-gradient-to-br from-primary-500/10 to-secondary-500/10" />

          <div class="relative">
            <div class="text-2xl sm:text-3xl md:text-4xl font-bold bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent mb-1.5 sm:mb-2 group-hover:scale-110 transition-transform duration-300 break-words">
              {{ stat.value }}
            </div>
            <div class="text-xs sm:text-sm text-gray-400 break-words leading-tight">{{ stat.label }}</div>
          </div>

          <!-- Corner accent with rounded border -->
          <div class="absolute bottom-0 right-0 w-10 h-10 border-r-2 border-b-2 border-secondary-500/50 rounded-br-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
        </div>
      </div>
    </div>

    <!-- Section corner decorations -->
    <div class="absolute top-0 right-0 w-40 h-40 border-r-2 border-t-2 border-secondary-500/20 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-40 h-40 border-l-2 border-b-2 border-primary-500/20 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * Testimonials carousel using Swiper
 * Clean design with gradient cards
 */

import { Star, Quote, Building2 } from 'lucide-vue-next'
import { Swiper, SwiperSlide } from 'swiper/vue'
import { Autoplay, Pagination } from 'swiper/modules'
import 'swiper/css'
import 'swiper/css/pagination'

import { testimonials } from '~/lib/data'
import { getInitials } from '~/utils/design'

const { t } = useI18n()

const modules = [Autoplay, Pagination]

/** Map testimonials with i18n translations */
const displayedTestimonials = computed(() => testimonials.map(testimonial => ({
  ...testimonial,
  text: t(`testimonials.items.${testimonial.id}.text`),
  position: t(`testimonials.items.${testimonial.id}.position`)
})))

/** Gradient backgrounds */
const getTestimonialGradient = (index: number) => {
  const gradients = [
    'linear-gradient(135deg, #1e3a5f 0%, #2d1f5e 100%)',
    'linear-gradient(135deg, #1a365d 0%, #44337a 100%)',
    'linear-gradient(135deg, #1e4d5c 0%, #2d3a5e 100%)',
    'linear-gradient(135deg, #2d2d5e 0%, #1e3a5f 100%)',
    'linear-gradient(135deg, #1a4731 0%, #234e52 100%)',
    'linear-gradient(135deg, #4a1d3d 0%, #2d1f5e 100%)'
  ]
  return gradients[index % gradients.length]
}

/** Stats */
const testimonialStats = computed(() => [
  { value: '98%', label: t('testimonials.stats.satisfaction') },
  { value: '150+', label: t('testimonials.stats.projects') },
  { value: '50+', label: t('testimonials.stats.clients') },
  { value: '4.9', label: t('testimonials.stats.rating') }
])
</script>

<style scoped>
.testimonials-swiper {
  padding-bottom: 50px;
}

:deep(.swiper-pagination) {
  bottom: 0;
}

:deep(.swiper-pagination-bullet) {
  width: 8px;
  height: 8px;
  background: #4b5563;
  opacity: 1;
  transition: all 0.3s ease;
  border-radius: 4px;
}

:deep(.swiper-pagination-bullet-active) {
  width: 32px;
  background: linear-gradient(90deg, #0ea5e9, #a855f7);
}
</style>
