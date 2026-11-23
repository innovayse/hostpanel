<template>
  <UiSection v-if="items.length" padding="lg" :contained="false" class="relative border-t border-line">
    <UiStack direction="row" wrap align="center" justify="between">
      <UiStack gap="none">
        <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac3']">{{ t('aurora.testimonials.eyebrow') }}</UiText>
        <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
          {{ t('aurora.testimonials.title') }}
        </UiHeading>
      </UiStack>
      <UiStack v-if="hasControls" direction="row" gap="xs" :class="controlsClass" data-testid="testimonials-arrows">
        <UiButton
          variant="bare"
          :aria-label="t('common.previous')"
          data-testid="testimonials-prev"
          class="h-10 w-10 rounded-full border border-line2 text-tx hover:border-ac1 hover:text-ac1"
          @click="go(-1)"
        >
          <ArrowLeft :size="16" />
        </UiButton>
        <UiButton
          variant="bare"
          :aria-label="t('common.next')"
          data-testid="testimonials-next"
          class="h-10 w-10 rounded-full border border-line2 text-tx hover:border-ac1 hover:text-ac1"
          @click="go(1)"
        >
          <ArrowRight :size="16" />
        </UiButton>
      </UiStack>
    </UiStack>

    <!-- The carousel region: arrow keys move it, so it is reachable without the pointer. -->
    <UiStack
      gap="none"
      role="region"
      aria-roledescription="carousel"
      :aria-label="t('aurora.testimonials.title')"
      data-testid="testimonials-track"
      class="mt-8"
      @keydown.left.prevent="go(-1)"
      @keydown.right.prevent="go(1)"
    >
      <!-- Up to three compact cards: the second shows from `sm`, the third from `lg`. Fewer cards than columns stay centred at their normal width. -->
      <UiStack direction="row" wrap justify="center" gap="md">
        <UiCard
          v-for="(item, slot) in visible"
          :key="item.id"
          tone="bare"
          padding="none"
          role="group"
          aria-roledescription="slide"
          :aria-label="t('aurora.testimonials.slide', { current: item.position, total: items.length })"
          data-testid="testimonial-card"
          class="w-full flex-col border-line2 bg-card p-5 shadow-panel sm:w-[calc((100%-1.25rem)/2)] lg:w-[calc((100%-2.5rem)/3)]"
          :class="SLOT_CLASSES[slot]"
        >
          <UiStack
            direction="row"
            gap="none"
            role="img"
            :aria-label="t('aurora.testimonials.rating', { rating: item.rating })"
            data-testid="testimonial-rating"
          >
            <Star
              v-for="n in MAX_RATING"
              :key="n"
              :size="14"
              aria-hidden="true"
              :class="n <= item.rating ? 'fill-current text-amber-500' : 'fill-current text-mut2 opacity-30'"
            />
          </UiStack>

          <UiText :as="UiTextTag.Blockquote" size="inherit" tone="inherit" class="mt-3 text-sm leading-relaxed text-tx">
            {{ item.quote }}
          </UiText>

          <UiStack direction="row" align="center" gap="sm" class="mt-auto border-t border-line pt-4">
            <UiImage
              v-if="item.avatarUrl"
              :src="item.avatarUrl"
              :alt="item.name"
              :width="40"
              :height="40"
              unoptimised
              rounded="full"
              class="h-10 w-10 shrink-0 ring-2 ring-line2"
            />
            <UiTile v-else size="lg" rounded class="bg-brand font-bold uppercase text-on-brand" data-testid="testimonial-initials">
              {{ initialsOf(item.name) }}
            </UiTile>
            <UiStack gap="none" class="min-w-0">
              <UiStack direction="row" wrap align="center" gap="xs">
                <UiText :as="UiTextTag.Span" size="sm" tone="inherit" weight="semibold" class="text-tx">{{ item.name }}</UiText>
                <UiText
                  v-if="item.date"
                  :as="UiTextTag.Time"
                  size="xs"
                  tone="inherit"
                  :datetime="item.publishedAt"
                  data-testid="testimonial-date"
                  class="text-mut2"
                >
                  {{ item.date }}
                </UiText>
              </UiStack>
              <UiText v-if="item.attribution" :as="UiTextTag.Span" size="xs" tone="inherit" class="text-mut">{{ item.attribution }}</UiText>
            </UiStack>
          </UiStack>
        </UiCard>
      </UiStack>

      <UiStack v-if="hasControls" direction="row" gap="xs" align="center" justify="center" class="mt-6" :class="controlsClass" data-testid="testimonials-dots">
        <UiButton
          v-for="(item, i) in items"
          :key="item.id"
          variant="bare"
          :aria-label="t('aurora.testimonials.goTo', { name: item.name })"
          :aria-current="i === index ? 'true' : undefined"
          :data-testid="`testimonials-dot-${i}`"
          class="relative h-2 rounded-full p-0 transition-all after:absolute after:-inset-2 after:content-['']"
          :class="i === index ? 'w-8 bg-ac1' : 'w-2 bg-mut2 opacity-50 hover:opacity-80'"
          @click="index = i"
        />
      </UiStack>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora testimonials carousel. The quotes come from the storefront store
 * (`GET /api/public/testimonials`); with none the section renders nothing.
 *
 * Up to three cards show at once (one on phones, two from `sm`, three from `lg`) and the
 * arrows, the dots and the left/right keys all move the window one quote at a time, wrapping at
 * both ends; the arrows and dots show only when there are more quotes than cards on screen. Fewer
 * quotes than columns stay centred at the normal card width. Each compact card carries the star rating, the author's avatar (or
 * initials on the brand gradient), role, company and the month the quote was given.
 */
import { ArrowLeft, ArrowRight, Star } from 'lucide-vue-next'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import { useStorefrontStore } from '~/stores/storefront'
import { formatTestimonialDate } from '~/utils/testimonialDate'
import type { Testimonial } from '~/types/testimonial'
import { UiTextTag } from '~/utils/ui/enums'

/** A quote prepared for its card. */
interface TestimonialSlide extends Testimonial {
  /** 1-based position in the full list, for the slide label. */
  position: number
  /** Role and company joined; empty when it has neither. */
  attribution: string
  /** The localised month and year; empty when the date is unusable. */
  date: string
}

/** Translator and the active language. */
const { t, locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:testimonials', () => storefront.loadTestimonials(), { watch: [locale] })

/** Highest star rating; the stars drawn per card. */
const MAX_RATING = 5

/** Most cards on screen at once (the `lg` layout). */
const MAX_VISIBLE = 3

/**
 * Visibility per card slot: the first always shows, the second from `sm`, the third from `lg`.
 * Matches the column presets of `UiGrid`, so a hidden card never leaves a gap.
 */
const SLOT_CLASSES: string[] = ['flex', 'hidden sm:flex', 'hidden lg:flex']

/** The quotes, in display order. */
const items = computed(() => storefront.testimonials)

/** Index of the quote that leads the window. */
const index = ref(0)

/** Whether any breakpoint has more quotes than cards on screen, so the arrows and dots are needed. */
const hasControls = computed(() => items.value.length > 1)

/**
 * Hides the arrows and dots at the breakpoints where every quote already fits: one card shows on
 * phones, two from `sm`, three from `lg`.
 */
const controlsClass = computed(() => {
  if (items.value.length > MAX_VISIBLE) {
    return ''
  }
  return items.value.length === MAX_VISIBLE ? 'lg:hidden' : 'sm:hidden'
})

/** The cards in the window, starting at the leading quote and wrapping past the end. */
const visible = computed<TestimonialSlide[]>(() => {
  const total = items.value.length
  const count = Math.min(MAX_VISIBLE, total)
  return Array.from({ length: count }, (_, offset) => (index.value + offset) % total).flatMap((at) => {
    const item = items.value[at]
    // `at` is always in range; the guard narrows the index read for the type checker.
    if (!item) {
      return []
    }
    return [{
      ...item,
      position: at + 1,
      attribution: [item.role, item.company].filter(Boolean).join(', '),
      date: formatTestimonialDate(item.publishedAt, locale.value),
    }]
  })
})

/**
 * Up to two initials of a name, for the avatar when there is no picture.
 *
 * @param name The author's display name.
 * @returns The first letters of the first two words, or `?` for an empty name.
 */
const initialsOf = (name: string): string =>
  name.trim().split(/\s+/).slice(0, 2).map(word => word.charAt(0)).join('') || '?'

/**
 * Moves the carousel, wrapping at both ends.
 *
 * @param step Positions to advance; negative moves back.
 */
const go = (step: number): void => {
  const total = items.value.length
  if (!total) {
    return
  }
  index.value = (index.value + step + total) % total
}
</script>
