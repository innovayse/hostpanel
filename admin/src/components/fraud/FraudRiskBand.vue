<script setup lang="ts">
/**
 * The 0 to 100 risk scale drawn as three bands, so the two thresholds are read as what they do: a score below the review
 * threshold is allowed, from it the order waits for review, from the reject threshold the action is refused.
 *
 * The bar is a live preview of the thresholds the form holds (an out of range or inverted pair is pulled onto the scale,
 * the form flags it under its field); under the bar each band is named, in its colour, with the scores it covers.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiChartDistribution from '@/components/ui/UiChartDistribution.vue'
import UiChartLegend from '@/components/ui/UiChartLegend.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { FraudBand, fraudRiskBands } from '@/lib/fraud'
import type { FraudBandSegment } from '@/lib/fraud'
import type { UiChartPart } from '@/utils/ui/chart'
import { UiGap } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for FraudRiskBand. */
const props = defineProps<{
  /** The review threshold. */
  review: number
  /** The reject threshold. */
  reject: number
}>()

/** Colour of each band, keying its stretch of the bar to its caption. */
const BAND_TONES: Record<FraudBand, UiTone> = {
  [FraudBand.Allow]: UiTone.Success,
  [FraudBand.Review]: UiTone.Warning,
  [FraudBand.Reject]: UiTone.Danger,
}

/** The bands the thresholds cut. */
const bands = computed<FraudBandSegment[]>(() => fraudRiskBands(props.review, props.reject))

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

/** Each band as a part of the bar, as wide as the share of the scale it takes (an empty band is not drawn). */
const parts = computed<UiChartPart[]>(() =>
  bands.value.map(segment => ({ count: segment.share, tone: BAND_TONES[segment.band], tooltip: captionOf(segment) })),
)

/** The key under the bar: each band's colour and caption. */
const legend = computed<{ tone: UiTone; label: string }[]>(() =>
  bands.value.map(segment => ({ tone: BAND_TONES[segment.band], label: captionOf(segment) })),
)
</script>

<template>
  <UiStack :gap="UiGap.Sm" data-testid="fraud-risk-band">
    <UiChartDistribution :parts="parts" wide />
    <UiChartLegend :items="legend" />
  </UiStack>
</template>
