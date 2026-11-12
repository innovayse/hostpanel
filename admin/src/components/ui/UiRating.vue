<script setup lang="ts">
/**
 * A rating as five stars followed by the average and the number of reviews, such as "4.5 (12)".
 *
 * The stars are decorative: the whole row carries one sentence for assistive technology ("Rated 4.5
 * out of 5 from 12 reviews"). The filled stars are the average rounded to the nearest whole star.
 * The component draws what it is given; whoever has no rating does not render it.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'

/** Number of stars in the row, and the highest score. */
const STAR_COUNT = 5

/** Props for UiRating. */
const props = defineProps<{
  /** Average score, expected from 1 to 5; the stars are clamped to the row anyway. */
  average: number
  /** How many reviews the average comes from. */
  count: number
}>()

/** Translation function and the interface language. */
const { t, locale } = useI18n()

/** The average with one decimal, as shown and as read out. */
const averageText = computed<string>(() =>
  new Intl.NumberFormat(locale.value, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(props.average),
)

/** How many stars are filled: the average rounded, kept inside the row. */
const filled = computed<number>(() => Math.min(Math.max(Math.round(props.average), 0), STAR_COUNT))

/** The sentence assistive technology reads for the whole row. */
const label = computed<string>(() => t('ui.rating.label', { average: averageText.value, count: props.count }, props.count))

/** The visible summary beside the stars. */
const summary = computed<string>(() => t('ui.rating.summary', { average: averageText.value, count: props.count }))

/** The five stars, each with whether it is filled. */
const stars = computed<{ index: number; on: boolean }[]>(() =>
  Array.from({ length: STAR_COUNT }, (_, index) => ({ index, on: index < filled.value })),
)
</script>

<template>
  <UiStack direction="row" align="center" gap="xs" role="img" :aria-label="label" data-testid="rating">
    <UiStack direction="row" align="center" gap="none">
      <UiIcon
        v-for="star in stars"
        :key="star.index"
        :markup="Icon.Star"
        size="sm"
        :class="star.on ? 'fill-current text-warning-500' : 'text-text-muted'"
        :data-on="star.on"
        data-testid="rating-star"
      />
    </UiStack>
    <UiText tone="muted" size="xs" aria-hidden="true" data-testid="rating-summary">{{ summary }}</UiText>
  </UiStack>
</template>
