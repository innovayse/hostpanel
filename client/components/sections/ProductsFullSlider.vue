<template>
  <section ref="sectionRef" class="relative h-[75vh] sm:h-[85vh] md:h-screen w-full overflow-hidden">
    <ClientOnly>
      <!-- SSR / pre-hydration hero — full first slide rendered immediately -->
      <template #fallback>
        <div class="absolute inset-0">
          <NuxtImg
            :src="sliderItems[0]?.image || '/images/products/hosting.jpg'"
            :alt="sliderItems[0]?.name || 'Innovayse'"
            width="1920"
            height="1080"
            format="webp"
            quality="80"
            sizes="100vw"
            :preload="true"
            class="h-full w-full object-cover"
          />
          <div class="absolute inset-0 bg-gradient-to-b from-black/80 via-black/40 to-black/90" />
        </div>
        <!-- First slide content in SSR fallback so the page isn't blank before hydration -->
        <div v-if="sliderItems[0]" class="container-custom relative z-10 flex h-full items-center justify-center">
          <div class="max-w-4xl text-center rounded-[2rem] sm:rounded-[3rem] bg-black/30 backdrop-blur-xl p-6 sm:p-10 md:p-14 lg:p-16 border border-white/10 shadow-[0_0_50px_rgba(0,0,0,0.5)] mb-10 sm:mb-0">
            <div
              class="mb-6 sm:mb-8 inline-flex items-center justify-center rounded-2xl sm:rounded-3xl p-4 sm:p-6 border-2"
              :style="{ backgroundColor: `${sliderItems[0].color}20`, borderColor: `${sliderItems[0].color}50` }"
            >
              <Icon :name="`mdi:${sliderItems[0].icon}`" class="text-5xl sm:text-6xl md:text-7xl" :style="{ color: sliderItems[0].color }" />
            </div>
            <h2 class="mb-4 sm:mb-6 text-3xl sm:text-5xl md:text-6xl lg:text-7xl font-black text-white tracking-tight leading-none">
              {{ sliderItems[0].name }}
            </h2>
            <p class="mb-4 sm:mb-6 text-lg sm:text-xl md:text-2xl text-primary-300 font-semibold tracking-wide uppercase italic">
              {{ sliderItems[0].tagline }}
            </p>
            <p class="mb-6 sm:mb-8 text-sm sm:text-base md:text-lg text-gray-300 leading-relaxed font-light line-clamp-3 px-4">
              {{ sliderItems[0].description }}
            </p>
            <div class="flex flex-col sm:flex-row justify-center gap-4 sm:gap-6 mt-8">
              <NuxtLink
                :to="localePath(sliderItems[0].learnMoreUrl)"
                class="w-full sm:w-auto inline-flex items-center justify-center gap-2 sm:gap-3 rounded-xl px-4 py-3 sm:px-10 sm:py-4 font-bold text-white text-sm sm:text-lg"
                :style="{ backgroundColor: sliderItems[0].color }"
              >
                {{ $t('products.pricing.cta.learnMore') }}
                <ArrowRight :size="18" :stroke-width="3" />
              </NuxtLink>
            </div>
          </div>
        </div>
      </template>

      <Swiper
        :key="sliderItems.length"
        :modules="modules"
        :slides-per-view="1"
        :space-between="0"
        :autoplay="sliderItems.length > 1 ? { delay: 5000, disableOnInteraction: false } : false"
        :navigation="{
          nextEl: '.swiper-button-next-custom',
          prevEl: '.swiper-button-prev-custom',
        }"
        :pagination="{ clickable: true }"
        :loop="sliderItems.length > 1"
        :speed="800"
        class="h-full w-full"
      >
        <SwiperSlide
          v-for="(item, index) in sliderItems"
          :key="item.id"
          class="relative h-full w-full"
        >
          <!-- Background with Ken Burns effect -->
          <div
            class="absolute inset-0 overflow-hidden"
            :style="!item.image ? { background: `radial-gradient(ellipse at 60% 40%, ${item.color}30 0%, #0a0a0f 70%)` } : {}"
          >
            <img
              v-if="item.image"
              :src="item.image"
              :alt="item.name"
              :loading="index === 0 ? 'eager' : 'lazy'"
              :fetchpriority="index === 0 ? 'high' : 'auto'"
              class="h-full w-full object-cover animate-ken-burns"
            />
            <!-- Overlay with noise/depth -->
            <div class="absolute inset-0 bg-gradient-to-b from-black/80 via-black/40 to-black/90" />
            <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://grainy-gradients.vercel.app/noise.svg')]" />
          </div>

          <!-- Content -->
          <div class="container-custom relative z-10 flex h-full items-center justify-center">
            <div 
              class="max-w-4xl text-center rounded-[2rem] sm:rounded-[3rem] bg-black/30 backdrop-blur-xl p-6 sm:p-10 md:p-14 lg:p-16 border border-white/10 shadow-[0_0_50px_rgba(0,0,0,0.5)] mb-10 sm:mb-0"
              v-motion
              :initial="{ opacity: 0, scale: 0.9, y: 30 }"
              :enter="{ opacity: 1, scale: 1, y: 0, transition: { duration: 800, type: 'spring' } }"
            >
              <!-- Icon with floating effect -->
              <div
                v-motion
                :initial="{ opacity: 0, scale: 0.5, rotate: -20 }"
                :enter="{ opacity: 1, scale: 1, rotate: 0, transition: { delay: 200, duration: 600 } }"
                class="mb-6 sm:mb-8 inline-flex items-center justify-center rounded-2xl sm:rounded-3xl p-4 sm:p-6 border-2 animate-float"
                :style="{
                  backgroundColor: `${item.color}20`,
                  borderColor: `${item.color}50`,
                  boxShadow: `0 20px 60px ${item.color}40`,
                  animationDelay: `${index * 0.2}s`
                }"
              >
                <Icon
                  :name="`mdi:${item.icon}`"
                  class="text-5xl sm:text-6xl md:text-7xl drop-shadow-[0_0_15px_rgba(255,255,255,0.3)]"
                  :style="{ color: item.color }"
                />
              </div>

              <!-- Name -->
              <h2
                v-motion
                :initial="{ opacity: 0, y: 20 }"
                :enter="{ opacity: 1, y: 0, transition: { delay: 400, duration: 600 } }"
                class="mb-4 sm:mb-6 text-3xl sm:text-5xl md:text-6xl lg:text-7xl font-black text-white tracking-tight leading-none"
                style="text-shadow: 0 10px 30px rgba(0,0,0,0.5)"
              >
                {{ item.name }}
              </h2>

              <!-- Tagline -->
              <p
                v-motion
                :initial="{ opacity: 0, y: 15 }"
                :enter="{ opacity: 1, y: 0, transition: { delay: 550, duration: 600 } }"
                class="mb-4 sm:mb-6 text-lg sm:text-xl md:text-2xl text-primary-300 font-semibold tracking-wide uppercase italic ml-2"
              >
                {{ item.tagline }}
              </p>

              <!-- Description -->
              <p
                v-motion
                :initial="{ opacity: 0, y: 15 }"
                :enter="{ opacity: 1, y: 0, transition: { delay: 700, duration: 600 } }"
                class="mb-6 sm:mb-8 text-sm sm:text-base md:text-lg lg:text-xl text-gray-300 leading-relaxed font-light line-clamp-3 md:line-clamp-none px-4"
              >
                {{ item.description }}
              </p>

              <!-- Features with staggered entrance -->
              <div v-if="item.features?.length" class="mb-8 mt-4 flex flex-wrap justify-center gap-3">
                <span
                  v-for="(feat, fIdx) in item.features"
                  :key="feat"
                  v-motion
                  :initial="{ opacity: 0, x: -10 }"
                  :enter="{ opacity: 1, x: 0, transition: { delay: 800 + fIdx * 100 } }"
                  class="group/badge inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-xs sm:text-sm font-bold backdrop-blur-md border transition-all hover:scale-110 cursor-default"
                  :style="{
                    backgroundColor: `${item.color}25`,
                    borderColor: `${item.color}50`,
                    color: 'white'
                  }"
                >
                  <div class="relative">
                    <CheckCircle :size="14" :stroke-width="3" :style="{ color: item.color }" class="relative z-10" />
                    <div class="absolute inset-0 bg-white blur-sm opacity-0 group-hover/badge:opacity-50 transition-opacity" />
                  </div>
                  {{ feat }}
                </span>
              </div>

              <!-- CTA Buttons -->
              <div 
                v-motion
                :initial="{ opacity: 0, y: 20 }"
                :enter="{ opacity: 1, y: 0, transition: { delay: 1000, duration: 600 } }"
                class="flex flex-col sm:flex-row justify-center gap-4 sm:gap-6 mt-8"
              >
                <UiMagnetic :strength="0.2">
                  <NuxtLink
                    :to="localePath(item.learnMoreUrl)"
                    class="w-full sm:w-auto group inline-flex items-center justify-center gap-2 sm:gap-3 rounded-xl px-4 py-3 sm:px-10 sm:py-4 font-bold text-white transition-all hover:scale-105 hover:shadow-2xl relative overflow-hidden text-sm sm:text-lg"
                    :style="{
                      backgroundColor: item.color,
                      boxShadow: `0 15px 40px ${item.color}50`
                    }"
                  >
                    <div class="absolute inset-0 bg-white/20 translate-y-full group-hover:translate-y-0 transition-transform duration-500" />
                    <span class="relative truncate">{{ $t('products.pricing.cta.learnMore') }}</span>
                    <ArrowRight :size="18" :stroke-width="3" class="transition-transform group-hover:translate-x-2 relative flex-shrink-0" />
                  </NuxtLink>
                </UiMagnetic>

                <UiMagnetic :strength="0.15" v-if="item.demoUrl">
                  <a
                    :href="item.demoUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="w-full sm:w-auto group inline-flex items-center justify-center gap-2 sm:gap-3 rounded-xl border-2 border-white/20 bg-white/5 px-4 py-3 sm:px-10 sm:py-4 font-bold text-white backdrop-blur-xl transition-all hover:scale-105 hover:border-white/40 hover:bg-white/10 text-sm sm:text-lg"
                  >
                    <span class="truncate">{{ $t('products.pricing.cta.viewDemo') }}</span>
                    <PlayCircle :size="18" :stroke-width="2.5" class="transition-transform group-hover:scale-125 text-primary-400 flex-shrink-0" />
                  </a>
                </UiMagnetic>
              </div>
            </div>
          </div>
        </SwiperSlide>
      </Swiper>

      <!-- Nav arrows — desktop only -->
      <button
        v-show="sliderItems.length > 1"
        class="swiper-button-prev-custom absolute left-4 top-1/2 z-20 -translate-y-1/2 rounded-full bg-white/15 p-2 text-white backdrop-blur-md transition-all hover:bg-white/30 hover:scale-110 border border-white/20 md:left-8 md:p-3 hidden md:flex items-center justify-center"
        :aria-label="$t('common.previous')"
      >
        <ChevronLeft :size="24" :stroke-width="2" />
      </button>
      <button
        v-show="sliderItems.length > 1"
        class="swiper-button-next-custom absolute right-4 top-1/2 z-20 -translate-y-1/2 rounded-full bg-white/15 p-2 text-white backdrop-blur-md transition-all hover:bg-white/30 hover:scale-110 border border-white/20 md:right-8 md:p-3 hidden md:flex items-center justify-center"
        :aria-label="$t('common.next')"
      >
        <ChevronRight :size="24" :stroke-width="2" />
      </button>
    </ClientOnly>

    <!-- Scroll Down — hidden on mobile to avoid overlap -->
    <button
      class="absolute bottom-10 left-1/2 z-30 -translate-x-1/2 animate-bounce rounded-full bg-white/10 backdrop-blur-md p-2 border border-white/20 hidden sm:flex items-center justify-center hover:bg-white/25 transition-colors"
      :aria-label="$t('common.scrollDown')"
      @click="scrollDown"
    >
      <ChevronDown :size="20" :stroke-width="2" class="text-white/80" />
    </button>
  </section>
</template>

<script setup lang="ts">
/**
 * Full-screen product slider hero section.
 *
 * Fetches slides from the backend API and displays them in a Swiper carousel
 * with Ken Burns background, floating icon animations, and staggered feature badges.
 */
import { ArrowRight, PlayCircle, ChevronLeft, ChevronRight, ChevronDown, CheckCircle } from 'lucide-vue-next'
import { Swiper, SwiperSlide } from 'swiper/vue'
import { Autoplay, Navigation, Pagination } from 'swiper/modules'
import 'swiper/css'
import 'swiper/css/navigation'
import 'swiper/css/pagination'

/** Shape of the slide DTO returned by the backend. */
interface SlidePublicDto {
  /** Unique slide identifier. */
  id: number
  /** MDI icon name (without the `mdi:` prefix). */
  iconName: string
  /** Hex brand color for the slide. */
  brandColor: string
  /** Background image path. */
  imageUrl: string
  /** Optional live demo URL. */
  demoUrl: string | null
  /** URL for the "Learn More" page. */
  learnMoreUrl: string | null
  /** Associated product ID, if any. */
  productId: number | null
  /** Monthly price, if applicable. */
  monthlyPrice: number | null
  /** Annual price, if applicable. */
  annualPrice: number | null
  /** Resolved translated title. */
  title: string
  /** Short tagline text. */
  tagline: string | null
  /** Longer description text. */
  description: string | null
  /** Feature bullet points. */
  features: string[] | null
  /** Call-to-action button text. */
  ctaText: string | null
  /** Call-to-action button URL. */
  ctaUrl: string | null
}

const { locale } = useI18n()
const localePath = useLocalePath()
const modules = [Autoplay, Navigation, Pagination]
const sectionRef = ref<HTMLElement | null>(null)

/** Fetch slides from the backend API. */
const { data: slidesRaw } = await useApi<SlidePublicDto[]>('/api/portal/public/slides', {
  query: computed(() => ({ lang: locale.value })),
  default: () => []
})

/** Maps API slide data to the shape expected by the template. */
const sliderItems = computed(() => {
  const slides = slidesRaw.value ?? []

  return slides.map((slide) => ({
    id: String(slide.id),
    name: slide.title,
    tagline: slide.tagline ?? '',
    description: slide.description ?? '',
    features: slide.features ?? [],
    icon: slide.iconName,
    color: slide.brandColor,
    image: slide.imageUrl,
    demoUrl: slide.demoUrl ?? '',
    learnMoreUrl: slide.learnMoreUrl ?? slide.ctaUrl ?? '/'
  }))
})

/**
 * Scrolls the viewport down to the bottom of this section.
 */
function scrollDown() {
  const section = sectionRef.value
  if (!section) return
  const bottom = section.getBoundingClientRect().bottom + window.scrollY
  window.scrollTo({ top: bottom, behavior: 'smooth' })
}
</script>

<style scoped>
:deep(.swiper-pagination) {
  bottom: 20px !important;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  pointer-events: auto;
}

:deep(.swiper-pagination-bullet) {
  width: 6px !important;
  height: 6px !important;
  border-radius: 9999px;
  background: rgba(255,255,255,0.45) !important;
  opacity: 1 !important;
  transition: all 0.3s ease;
  cursor: pointer;
  pointer-events: auto !important;
  padding: 4px;
  box-sizing: content-box;
}

:deep(.swiper-pagination-bullet:hover) {
  background: rgba(255,255,255,0.7) !important;
}

:deep(.swiper-pagination-bullet-active) {
  background: white !important;
  transform: scale(1.2);
}

:deep(.swiper-slide) h2,
:deep(.swiper-slide) h3,
:deep(.swiper-slide) p {
  line-height: 1.2;
  font-family: "Inter", sans-serif;
}

:deep(.swiper-slide) span {
  line-height: 1.2;
  font-family: "Inter", sans-serif;
}
@keyframes ken-burns {
  0% { transform: scale(1); }
  50% { transform: scale(1.1); }
  100% { transform: scale(1); }
}

.animate-ken-burns {
  animation: ken-burns 20s ease-in-out infinite;
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-15px); }
}

.animate-float {
  animation: float 4s ease-in-out infinite;
}
</style>
