<template>
  <UiAccent :color="item.color">
    <!-- Split slide: the cover above the text on a phone, the text on the left and the cover on the right from md up. Both fill the slider height. -->
    <UiContainer class="relative z-10 grid h-full grid-cols-1 grid-rows-[minmax(0,2fr)_minmax(0,3fr)] items-stretch gap-5 pb-16 pt-20 md:grid-cols-2 md:grid-rows-1 md:gap-12 md:pb-16 md:pt-14" :data-testid="`slider-slide-${item.id}`">
      <!-- Cover -->
      <UiStack
        gap="none"
        :class="[accent.tintStrong, 'relative min-h-0 overflow-hidden rounded-2xl ring-1 ring-inset ring-[color:color-mix(in_srgb,var(--accent)_40%,transparent)] md:order-2']"
        :data-testid="`slider-cover-${item.id}`"
      >
        <UiImage
          v-if="cover.kind === SlideCoverKind.Image"
          :src="cover.src"
          :alt="item.name"
          :loading="eager ? 'eager' : 'lazy'"
          class="absolute inset-0 h-full w-full"
          :testid="`slider-cover-image-${item.id}`"
        />
        <UiDecor
          v-else
          :class="[accent.fill, cover.classes, 'inset-6 opacity-80']"
          :data-testid="`slider-cover-art-${item.id}`"
        />
      </UiStack>

      <!-- Text -->
      <UiStack gap="none" justify="center" class="min-h-0 md:order-1" :data-testid="`slider-text-${item.id}`">
        <UiAccentTile :icon="`mdi:${item.icon}`" size="lg" class="mb-4 sm:mb-6" />

        <UiHeading
          :level="2"
          class="mb-2 text-3xl font-bold leading-tight tracking-tight sm:mb-4 sm:text-4xl xl:text-5xl animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(400)"
        >
          {{ item.name }}
        </UiHeading>

        <UiText
          v-if="item.tagline"
          tone="primary"
          class="mb-2 text-base font-semibold sm:mb-4 sm:text-xl md:text-2xl animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(550)"
        >
          {{ item.tagline }}
        </UiText>

        <UiText
          v-if="item.description"
          tone="muted"
          class="mb-4 line-clamp-3 text-sm font-light leading-relaxed sm:mb-6 sm:text-base md:line-clamp-none md:text-lg animate-reveal-up motion-reduce:animate-none"
          :class="revealDelay(700)"
        >
          {{ item.description }}
        </UiText>

        <!-- Features, one after another -->
        <UiStack v-if="item.features.length" direction="row" wrap gap="xs" class="mb-5 sm:mb-8 sm:gap-3" :data-testid="`slider-features-${item.id}`">
          <UiText
            v-for="(feature, featureIndex) in item.features"
            :key="feature"
            :as="UiTextTag.Span"
            tone="inherit"
            size="xs"
            weight="bold"
            :class="[accent.tintStrong, accent.borderMedium, 'inline-flex cursor-default items-center gap-2 rounded-md border px-3 py-1 text-classic-ink sm:px-4 sm:py-1.5 sm:text-sm', 'animate-reveal-up motion-reduce:animate-none', revealDelay(800 + featureIndex * 100)]"
          >
            <CheckCircle :size="14" :stroke-width="3" :class="accent.text" />
            {{ feature }}
          </UiText>
        </UiStack>

        <!-- Calls to action -->
        <UiStack gap="sm" class="sm:flex-row sm:gap-4 animate-reveal-up motion-reduce:animate-none" :class="revealDelay(1000)">
          <UiMagnetic :strength="0.2">
            <UiAccentButton :to="localePath(item.learnMoreUrl)" appearance="solid" size="lg" class="w-full font-bold sm:w-auto sm:px-10 sm:py-4" :data-testid="`slider-cta-${item.id}`">
              <UiText :as="UiTextTag.Span" tone="inherit" size="inherit" weight="bold" truncate>{{ $t('common.learnMore') }}</UiText>
              <ArrowRight :size="18" :stroke-width="3" class="flex-shrink-0" />
            </UiAccentButton>
          </UiMagnetic>

          <UiMagnetic v-if="demoHref" :strength="0.15">
            <UiButton :href="demoHref" target="_blank" rel="noopener noreferrer" variant="subtle" size="lg" class="w-full font-bold sm:w-auto sm:px-10 sm:py-4" :data-testid="`slider-demo-${item.id}`">
              <UiText :as="UiTextTag.Span" tone="inherit" size="inherit" weight="bold" truncate>{{ $t('products.pricing.cta.viewDemo') }}</UiText>
              <PlayCircle :size="18" :stroke-width="2.5" class="ml-2 flex-shrink-0 text-primary-400" />
            </UiButton>
          </UiMagnetic>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiAccent>
</template>

<script setup lang="ts">
/**
 * One slide of the classic home slider, laid out as a split: the text block (icon chip, title, subtitle,
 * text, chips, buttons) beside a cover illustration that fills the slider height. On a phone the cover
 * sits above the text. The cover is the slide's uploaded picture, else the illustration matching its
 * topic (`slideCover`). The slide follows the colour mode; its own colour is the accent.
 */

import { ArrowRight, CheckCircle, PlayCircle } from 'lucide-vue-next'
import { revealDelay } from '~/utils/reveal'
import { slideCover } from '~/utils/productTypeCover'
import { safeHref } from '~/utils/safeUrl'
import { ACCENT_CLASSES } from '~/utils/ui/accent'
import { SlideCoverKind } from '~/types/slidecoverkind'
import type { SliderItem } from '~/types/slideritem'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of the slide. */
interface Props {
  /** The slide to show. */
  item: SliderItem
  /** Loads the cover picture at once, for the first slide. */
  eager?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), { eager: false })

/** Accent classes read from the slide's own colour. */
const accent = ACCENT_CLASSES
/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** The cover to draw beside the text. */
const cover = computed(() => slideCover(props.item.image, props.item.learnMoreUrl))

/** The demo address when it is a safe web link; the button is not drawn for anything else. */
const demoHref = computed(() => safeHref(props.item.demoUrl))
</script>
