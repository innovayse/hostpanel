<template>
  <!-- The panel is the page colour in either mode; the cover beside each slide carries the picture. -->
  <!-- No slides configured: the text hero, so the top of the home page is never an empty viewport. -->
  <SectionsHero v-if="!sliderItems.length" />
  <UiSection v-else :contained="false" padding="none" class="relative h-[90dvh] sm:h-[85dvh] md:h-[calc(100dvh-4.5rem)] md:max-h-[44rem] w-full overflow-hidden bg-classic-page" data-testid="products-slider">
    <ClientOnly>
      <!-- Before hydration: the first slide, rendered at once so the page is never blank -->
      <template #fallback>
        <SectionsProductsSlide v-if="firstSlide" :item="firstSlide" eager />
      </template>

      <Swiper
        :key="sliderItems.length"
        :modules="modules"
        :slides-per-view="1"
        :space-between="0"
        :autoplay="sliderItems.length > 1 ? { delay: 5000, disableOnInteraction: false } : false"
        :navigation="{ nextEl: '.swiper-button-next-custom', prevEl: '.swiper-button-prev-custom' }"
                :loop="sliderItems.length > 1"
        :speed="800"
        class="h-full w-full"
        @swiper="onSwiper"
        @slide-change="onSlideChange"
      >
        <SwiperSlide v-for="(item, index) in sliderItems" :key="item.id" class="relative h-full w-full">
          <SectionsProductsSlide :item="item" :eager="index === 0" />
        </SwiperSlide>
      </Swiper>

      <!-- Pagination: kit dots driven from the carousel (Swiper's own bullets are off) -->
      <UiCarouselDots
        v-if="sliderItems.length > 1"
        :count="sliderItems.length"
        :active="active"
        class="absolute bottom-5 left-1/2 z-20 -translate-x-1/2"
        @select="select"
      />

      <!-- Navigation arrows, desktop only -->
      <UiIconButton
        v-show="sliderItems.length > 1"
        :icon="ChevronLeft"
        :label="$t('common.previous')"
        variant="subtle"
        size="lg"
        class="swiper-button-prev-custom absolute left-4 md:left-8 top-1/2 z-20 -translate-y-1/2 rounded-md hidden md:flex"
        testid="slider-prev"
      />
      <UiIconButton
        v-show="sliderItems.length > 1"
        :icon="ChevronRight"
        :label="$t('common.next')"
        variant="subtle"
        size="lg"
        class="swiper-button-next-custom absolute right-4 md:right-8 top-1/2 z-20 -translate-y-1/2 rounded-md hidden md:flex"
        testid="slider-next"
      />
    </ClientOnly>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Full-screen product slider hero section.
 *
 * Fetches the slides from the backend through the catalogue store and shows them in a Swiper
 * carousel. Each slide is a split (`SectionsProductsSlide`): the text on the left and a cover
 * illustration on the right, the cover above the text on a phone. Every slide keeps its own brand colour
 * (`UiAccent`) and the panel follows the colour mode. Before the browser takes over, the first slide
 * is rendered by the server so the page is never blank. With no slide at all it renders the
 * text hero (`SectionsHero`) in its place, so the page never opens on an empty, full-height band.
 * The pagination is the kit's `UiCarouselDots`, driven from the carousel; Swiper's own bullets are off.
 */

import { ChevronLeft, ChevronRight } from 'lucide-vue-next'
import { Swiper, SwiperSlide } from 'swiper/vue'
import { Autoplay, Navigation } from 'swiper/modules'
import 'swiper/css'
import 'swiper/css/navigation'
import { useCarouselDots } from '~/composables/useCarouselDots'
import { useCatalogStore } from '~/stores/catalog'
import type { SliderItem } from '~/types/slideritem'

/** Translation helpers and the active language. */
const { locale } = useI18n()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

/** Swiper modules the carousel uses. */
const modules = [Autoplay, Navigation]

// The slides are re-read when the language changes.
await useAsyncData('home:slides', () => catalogStore.loadSlides(locale.value), { watch: [locale] })

/** The slides shaped for the template. */
const sliderItems = computed(() => (catalogStore.slides ?? []).map((slide): SliderItem => ({
  id: String(slide.id),
  name: slide.content.title,
  tagline: slide.content.tagline ?? '',
  description: slide.content.description ?? '',
  features: slide.content.features ?? [],
  icon: slide.iconName,
  color: slide.brandColor,
  image: slide.imageUrl,
  demoUrl: slide.links.demoUrl ?? '',
  learnMoreUrl: slide.links.learnMoreUrl ?? slide.callToAction.url ?? '/',
})))

/** The first slide, which the server-rendered fallback shows. */
const firstSlide = computed(() => sliderItems.value[0])

/** The dots' position and handlers, driven from the carousel. */
const { active, onSwiper, onSlideChange, select } = useCarouselDots()
</script>
