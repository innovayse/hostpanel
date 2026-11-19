<script setup lang="ts">
/**
 * The 0 to 100 risk scale drawn as three bands, so the two thresholds are read as what they do: a score below the review
 * threshold is allowed, from it the order waits for review, from the reject threshold the action is refused.
 *
 * The bar is a live preview of the thresholds the form holds (an out of range or inverted pair is pulled onto the scale,
 * the form flags it under its field); under the bar each band is named with the scores it covers. It is a single image
 * for assistive technology, described by the same three sentences.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiText from '@/components/ui/UiText.vue'
import { FRAUD_SCORE_MAX, FraudBand, fraudRiskBands } from '@/lib/fraud'
import type { FraudBandSegment } from '@/lib/fraud'

const { t } = useI18n()

/** Props for FraudRiskBand. */
const props = defineProps<{
  /** The review threshold. */
  review: number
  /** The reject threshold. */
  reject: number
}>()

/** Fill of each band's stretch of the bar. */
const FILL_CLASSES: Record<FraudBand, string> = {
  [FraudBand.Allow]: 'fill-status-green',
  [FraudBand.Review]: 'fill-status-yellow',
  [FraudBand.Reject]: 'fill-status-red',
}

/** Colour of the dot that keys each band's caption to the bar. */
const DOT_CLASSES: Record<FraudBand, string> = {
  [FraudBand.Allow]: 'bg-status-green',
  [FraudBand.Review]: 'bg-status-yellow',
  [FraudBand.Reject]: 'bg-status-red',
}

/** The bands the thresholds cut. */
const bands = computed<FraudBandSegment[]>(() => fraudRiskBands(props.review, props.reject))

/** Each band with the left edge of its stretch of the bar (an empty band has a stretch of no width). */
const stretches = computed<{ segment: FraudBandSegment; start: number }[]>(() =>
  bands.value.reduce<{ segment: FraudBandSegment; start: number }[]>((drawn, segment) => {
    const previous = drawn[drawn.length - 1]
    return [...drawn, { segment, start: previous === undefined ? 0 : previous.start + previous.segment.share }]
  }, []),
)

/**
 * The scores a band covers, in words.
 *
 * @param segment - The band.
 * @returns "0 to 49", or "none" when the thresholds leave the band empty.
 */
const rangeOf = (segment: FraudBandSegment): string =>
  segment.to < segment.from ? t('fraud.settings.band.empty') : t('fraud.settings.band.range', { from: segment.from, to: segment.to })

/**
 * The caption of a band: its name and the scores it covers.
 *
 * @param segment - The band.
 * @returns For example "Allow, 0 to 49".
 */
const captionOf = (segment: FraudBandSegment): string => `${t(`fraud.settings.band.${segment.band}`)}, ${rangeOf(segment)}`

/** The whole preview as one sentence for assistive technology. */
const summary = computed<string>(() => bands.value.map(captionOf).join('. '))
</script>

<template>
  <div class="flex flex-col gap-2" data-testid="fraud-risk-band">
    <div class="overflow-hidden rounded-full bg-surface-hover">
      <svg
        class="block h-3 w-full"
        :viewBox="`0 0 ${FRAUD_SCORE_MAX} 1`"
        preserveAspectRatio="none"
        role="img"
        :aria-label="summary"
      >
        <rect
          v-for="{ segment, start } in stretches"
          :key="segment.band"
          :x="start"
          y="0"
          :width="segment.share"
          height="1"
          :class="FILL_CLASSES[segment.band]"
          :data-testid="`fraud-risk-band-${segment.band}`"
        />
      </svg>
    </div>
    <ul class="grid grid-cols-1 gap-x-4 gap-y-1 sm:grid-cols-3" aria-hidden="true">
      <li v-for="segment in bands" :key="segment.band" class="flex items-center gap-2" :data-testid="`fraud-risk-band-caption-${segment.band}`">
        <span class="h-2.5 w-2.5 shrink-0 rounded-full" :class="DOT_CLASSES[segment.band]" />
        <UiText size="sm" tone="secondary">
          {{ captionOf(segment) }}
        </UiText>
      </li>
    </ul>
  </div>
</template>
